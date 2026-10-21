using System.Text;
using Hearthlens.Core.Hearthstone;

namespace Hearthlens.Core.Logs;

public enum TailStart
{
    Beginning,
    LastGame,
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
        _position = start == TailStart.LastGame && File.Exists(_path) ? FindLastGameOffset(_path) : 0;
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

    private static long FindLastGameOffset(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var data = new byte[stream.Length];
            stream.ReadExactly(data);
            var index = data.AsSpan().LastIndexOf(CreateGameMarker);
            if (index < 0)
            {
                return data.Length;
            }

            var lineStart = data.AsSpan(0, index).LastIndexOf((byte)'\n');
            return lineStart + 1;
        }
        catch (IOException)
        {
            return 0;
        }
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

    public LogWatcher(ILogConsumer consumer, Func<string?> installDir)
    {
        _consumer = consumer;
        _installDir = installDir;
    }

    public string? CurrentSession => _session;

    public void Start()
    {
        _thread = new Thread(Run) { IsBackground = true, Name = "Hearthlens log watcher" };
        _thread.Start();
    }

    private void Run()
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

            _cts.Token.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(100));
        }
    }

    private void Poll()
    {
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

        _consumer.OnTick();
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
