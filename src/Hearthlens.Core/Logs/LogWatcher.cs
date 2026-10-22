using System.Text;
using Hearthlens.Core.Hearthstone;

namespace Hearthlens.Core.Logs;

public enum TailStart
{
    Beginning,
    LastGame,
    End,
}

/// <summary>Incrementally reads complete lines appended to one log file.</summary>
public sealed class LogTail
{
    private static readonly byte[] CreateGameMarker = Encoding.ASCII.GetBytes("GameState.DebugPrintPower() - CREATE_GAME");

    private readonly string _fileName;
    private string? _path;
    private long _position;
    private byte[] _remainder = Array.Empty<byte>();

    public LogTail(string fileName)
    {
        _fileName = fileName;
    }

    public string FileName => _fileName;

    public void Reset(string sessionDir, TailStart start)
    {
        _path = Path.Combine(sessionDir, _fileName);
        _remainder = Array.Empty<byte>();
        _position = start switch
        {
            TailStart.LastGame when File.Exists(_path) => FindLastGameOffset(_path),
            TailStart.End when File.Exists(_path) => new FileInfo(_path).Length,
            _ => 0,
        };
    }

    /// <summary>Bytes appended to the file since the last read; 0 when the tail is caught up.</summary>
    public long PendingBytes
    {
        get
        {
            try
            {
                if (_path is null)
                {
                    return 0;
                }

                var info = new FileInfo(_path);
                return info.Exists ? Math.Max(0, info.Length - _position) : 0;
            }
            catch (IOException)
            {
                return 0;
            }
            catch (UnauthorizedAccessException)
            {
                return 0;
            }
        }
    }

    public List<string> ReadNewLines(int maxBytes = 8 * 1024 * 1024)
    {
        var lines = new List<string>();
        if (_path is null || !File.Exists(_path))
        {
            return lines;
        }

        try
        {
            using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (stream.Length < _position)
            {
                _position = 0;
                _remainder = Array.Empty<byte>();
            }

            var available = (int)Math.Min(stream.Length - _position, maxBytes);
            if (available <= 0)
            {
                return lines;
            }

            var buffer = new byte[_remainder.Length + available];
            _remainder.CopyTo(buffer, 0);
            stream.Seek(_position, SeekOrigin.Begin);
            stream.ReadExactly(buffer, _remainder.Length, available);
            _position += available;

            var start = 0;
            for (var i = 0; i < buffer.Length; i++)
            {
                if (buffer[i] != (byte)'\n')
                {
                    continue;
                }

                var end = i > start && buffer[i - 1] == (byte)'\r' ? i - 1 : i;
                lines.Add(Encoding.UTF8.GetString(buffer, start, end - start));
                start = i + 1;
            }

            _remainder = buffer[start..];
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        return lines;
    }

    /// <summary>
    /// Offset of the line holding the last CREATE_GAME, scanning backwards in 1 MB blocks so a long session's
    /// log is never loaded into memory in one piece (a mid-game log can be hundreds of megabytes).
    /// </summary>
    private static long FindLastGameOffset(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var length = stream.Length;
            const int chunkSize = 1024 * 1024;
            var position = length;
            while (position > 0)
            {
                var size = (int)Math.Min(chunkSize, position);
                var start = position - size;
                // Read a little past the block so a marker split by the boundary is still seen whole.
                var count = (int)Math.Min(size + CreateGameMarker.Length - 1, length - start);
                var chunk = new byte[count];
                stream.Seek(start, SeekOrigin.Begin);
                stream.ReadExactly(chunk);

                var index = chunk.AsSpan().LastIndexOf(CreateGameMarker);
                if (index >= 0)
                {
                    return LineStart(stream, start + index);
                }

                position = start;
            }

            return length;
        }
        catch (IOException)
        {
            return 0;
        }
    }

    /// <summary>Start of the line that contains <paramref name="offset"/>.</summary>
    private static long LineStart(FileStream stream, long offset)
    {
        var from = Math.Max(0, offset - 64 * 1024);
        var buffer = new byte[offset - from];
        stream.Seek(from, SeekOrigin.Begin);
        stream.ReadExactly(buffer);
        var index = buffer.AsSpan().LastIndexOf((byte)'\n');
        return index < 0 ? from : from + index + 1;
    }
}

public interface ILogConsumer
{
    /// <summary>Called when the client starts a new log session (new folder).</summary>
    void OnSessionStarted(string sessionDir, DateTime sessionStart, bool attachedMidSession);

    /// <summary>A batch of new lines from one file. <paramref name="catchingUp"/> is true for history read at startup.</summary>
    void OnLines(string fileName, IReadOnlyList<string> lines, bool catchingUp);

    /// <summary>Called after each polling pass.</summary>
    void OnTick();

    /// <summary>
    /// The watcher is about to replay the current game from its start (hard refresh). The tracked state is rebuilt
    /// from the replayed lines, so it must be dropped here without recording the game as finished.
    /// </summary>
    void OnResync() { }
}

/// <summary>Polls the active Hearthstone log session folder and streams new lines to a consumer.</summary>
public sealed class LogWatcher : IDisposable
{
    private readonly ILogConsumer _consumer;
    private readonly Func<string?> _installDir;
    private readonly LogTail[] _tails =
    [
        new("LoadingScreen.log"),
        new("Decks.log"),
        new("Power.log"),
    ];

    private readonly CancellationTokenSource _cts = new();
    private Thread? _thread;
    private string? _session;
    private DateTime _nextSessionCheck = DateTime.MinValue;
    private bool _firstSession = true;
    private int _resync;
    private long _lastPollTicks = Environment.TickCount64;
    private long _pendingBytes;

    public LogWatcher(ILogConsumer consumer, Func<string?> installDir)
    {
        _consumer = consumer;
        _installDir = installDir;
    }

    public string? CurrentSession => _session;

    /// <summary>Milliseconds since the last polling pass finished (a dead or wedged watcher stops growing this).</summary>
    public long LastPollAgeMs => Environment.TickCount64 - Volatile.Read(ref _lastPollTicks);

    /// <summary>Bytes the game's logs have that the watcher has not delivered yet.</summary>
    public long PendingBytes => Volatile.Read(ref _pendingBytes);

    /// <summary>
    /// Makes the next pass re-read the log. With <paramref name="replayCurrentGame"/> the current game is replayed
    /// from its CREATE_GAME line, which rebuilds the whole board and turn (the recovery for a frozen overlay); without
    /// it every file just jumps to its end.
    /// </summary>
    public void RequestResync(bool replayCurrentGame) => Interlocked.Exchange(ref _resync, replayCurrentGame ? 1 : 2);

    public void Start()
    {
        Volatile.Write(ref _lastPollTicks, Environment.TickCount64);
        _thread = new Thread(Run) { IsBackground = true, Name = "Hearthlens log watcher" };
        _thread.Start();
    }

    private void Run()
    {
        try
        {
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    Poll();
                }
                catch (Exception ex)
                {
                    Diag.Error("Log watcher pass failed", ex);
                }
                finally
                {
                    Volatile.Write(ref _lastPollTicks, Environment.TickCount64);
                }

                _cts.Token.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(100));
            }
        }
        catch (ObjectDisposedException)
        {
            // The watchdog restarted the watcher while this pass was still running: this thread is done.
        }
    }

    private void Poll()
    {
        var resync = Interlocked.Exchange(ref _resync, 0);
        if (resync != 0)
        {
            Resync(replayCurrentGame: resync == 1);
        }

        if (DateTime.UtcNow >= _nextSessionCheck)
        {
            _nextSessionCheck = DateTime.UtcNow.AddSeconds(2);
            var install = _installDir();
            var latest = install is null ? null : HearthstoneInstall.LatestLogSession(install);
            if (latest is not null && latest != _session)
            {
                // A session already running when we start is joined at its last game;
                // sessions created later are read from their first line.
                var midSession = _firstSession && File.Exists(Path.Combine(latest, "Power.log"));
                _firstSession = false;
                _session = latest;
                foreach (var tail in _tails)
                {
                    tail.Reset(latest, midSession && tail.FileName == "Power.log" ? TailStart.LastGame : TailStart.Beginning);
                }

                _consumer.OnSessionStarted(latest, HearthstoneInstall.SessionStart(latest) ?? DateTime.Now, midSession);
                CatchUp(midSession);
            }
        }

        if (_session is null)
        {
            Volatile.Write(ref _pendingBytes, 0);
            _consumer.OnTick();
            return;
        }

        foreach (var tail in _tails)
        {
            var lines = tail.ReadNewLines();
            if (lines.Count > 0)
            {
                _consumer.OnLines(tail.FileName, lines, catchingUp: false);
            }
        }

        var pending = 0L;
        foreach (var tail in _tails)
        {
            pending += tail.PendingBytes;
        }

        Volatile.Write(ref _pendingBytes, pending);
        _consumer.OnTick();
    }

    /// <summary>Re-attaches to the session folder and replays what is needed (see <see cref="RequestResync"/>).</summary>
    private void Resync(bool replayCurrentGame)
    {
        var install = _installDir();
        var latest = install is null ? null : HearthstoneInstall.LatestLogSession(install);
        if (latest is null)
        {
            return;
        }

        _session = latest;
        _firstSession = false;
        _nextSessionCheck = DateTime.UtcNow.AddSeconds(2);
        foreach (var tail in _tails)
        {
            if (replayCurrentGame && tail.FileName == "Power.log")
            {
                tail.Reset(latest, TailStart.LastGame);
            }
            else
            {
                tail.Reset(latest, TailStart.End);
            }
        }

        _consumer.OnResync();
        CatchUp(historical: true);
        Diag.Info(replayCurrentGame
            ? $"Tracking resynced: replaying the current game from {Path.GetFileName(latest)}"
            : $"Tracking resynced: jumped to the end of {Path.GetFileName(latest)}");
    }

    private void CatchUp(bool historical)
    {
        foreach (var tail in _tails)
        {
            List<string> lines;
            while ((lines = tail.ReadNewLines()).Count > 0)
            {
                _consumer.OnLines(tail.FileName, lines, catchingUp: historical);
            }
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _thread?.Join(TimeSpan.FromSeconds(1));
        _cts.Dispose();
    }
}
