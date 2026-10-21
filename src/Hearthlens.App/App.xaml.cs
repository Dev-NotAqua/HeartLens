using System.IO;
using System.Net;
using System.Net.Http;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Hearthlens.App.Infrastructure;
using Hearthlens.App.Main;
using Hearthlens.App.Overlay;
using Hearthlens.Core;
using Hearthlens.Core.Battlegrounds;
using Hearthlens.Core.Cards;
using Hearthlens.Core.Data;
using Hearthlens.Core.Hearthstone;
using Hearthlens.Core.Logs;
using Hearthlens.Core.Memory;
using Hearthlens.Core.Tracking;
using Forms = System.Windows.Forms;

namespace Hearthlens.App;

public partial class App : Application
{
    private const int HotkeyId = 0x484C;
    private const int LayoutHotkeyId = 0x484D;

    private Mutex? _mutex;
    private AppSettings _settings = new();
    private HttpClient _http = null!;
    private HistoryStore _history = null!;
    private HearthstoneMemory _memory = null!;
    private GameSession _session = null!;
    private LogWatcher? _watcher;
    private MainWindow _main = null!;
    private MainViewModel _mainViewModel = null!;
    private OverlayWindow _overlay = null!;
    private OverlayViewModel _overlayViewModel = null!;
    private Forms.NotifyIcon? _tray;
    private Forms.ToolStripMenuItem? _trayOverlayItem;
    private HwndSource? _hotkeySource;
    private string? _installDir;
    private CardDb _cards = CardDb.Empty;
    private LiveSnapshot? _pending;
    private LiveSnapshot _last = LiveSnapshot.Empty;
    private int _applyScheduled;
    private DispatcherTimer _historyDebounce = null!;
    private DispatcherTimer _settingsDebounce = null!;
    private DispatcherTimer _metaRefresh = null!;
    private string? _deckStatsKey;
    private DeckAggregate? _deckStats;
    private MatchupAggregate? _matchupStats;
    private string? _renderDir;
    private BattlegroundsMeta? _meta;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        for (var i = 0; i < e.Args.Length - 1; i++)
        {
            if (e.Args[i] == "--render-ui")
            {
                _renderDir = e.Args[i + 1];
            }
        }

        _mutex = new Mutex(true, @"Local\Hearthlens.SingleInstance", out var firstInstance);
        if (!firstInstance && _renderDir is null)
        {
            MessageBox.Show("Hearthlens is already running — look for it in the system tray.", "Hearthlens", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        DispatcherUnhandledException += (_, args) =>
        {
            Diag.Error("UI error", args.Exception);
            args.Handled = true;
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Diag.Error("Background error", args.Exception);
            args.SetObserved();
        };

        _settings = AppSettings.Load();
        _http = new HttpClient(new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
        })
        {
            Timeout = TimeSpan.FromSeconds(40),
        };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("Hearthlens/" + (typeof(App).Assembly.GetName().Version?.ToString(3) ?? "0.1"));
        CardArt.Http = _http;

        _history = new HistoryStore(AppPaths.Database);
        _memory = new HearthstoneMemory();
        _session = new GameSession(_history, _memory);

        _mainViewModel = new MainViewModel(_history, _settings);
        _overlayViewModel = new OverlayViewModel();
        _overlayViewModel.PinRequested += id => _session.PinComp(id);

        _main = new MainWindow(_mainViewModel);
        _main.SettingsChanged += OnSettingsChanged;
        _main.ReimportRequested += () => _ = ImportLogsAsync(force: true);
        _overlay = new OverlayWindow(_overlayViewModel, _settings);
        _overlay.LayoutChanged += OnSettingsChanged;
        _overlay.HoveredHero = () => _memory.IsAttached ? _memory.GetHoveredLeaderboardHero() : null;
        _main.ArrangeOverlayRequested += () => _overlayViewModel.LayoutEditing = !_overlayViewModel.LayoutEditing;
        if (_renderDir is not null)
        {
            PrepareOffscreenRender();
        }
        else
        {
            _overlay.Show();
            if (!_settings.StartMinimized)
            {
                _main.Show();
            }
        }

        _historyDebounce = Debouncer(TimeSpan.FromMilliseconds(600), () => _ = _mainViewModel.RefreshAsync());
        _settingsDebounce = Debouncer(TimeSpan.FromMilliseconds(400), _settings.Save);
        _metaRefresh = new DispatcherTimer { Interval = TimeSpan.FromHours(3) };
        _metaRefresh.Tick += (_, _) => _ = LoadMetaAsync();

        _session.SnapshotChanged += OnSnapshot;
        _session.GameFinished += outcome => Dispatcher.BeginInvoke(() => OnGameFinished(outcome));
        _history.Changed += () => Dispatcher.BeginInvoke(() =>
        {
            _historyDebounce.Stop();
            _historyDebounce.Start();
            _deckStatsKey = null;
        });

        if (_renderDir is null)
        {
            SetupTray();
            RegisterHotkey();
        }

        _ = InitializeAsync();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _watcher?.Dispose();
        if (_hotkeySource is not null)
        {
            Native.UnregisterHotKey(_hotkeySource.Handle, HotkeyId);
            Native.UnregisterHotKey(_hotkeySource.Handle, LayoutHotkeyId);
        }

        if (_tray is not null)
        {
            _tray.Visible = false;
            _tray.Dispose();
        }

        _memory?.Dispose();
        _settings.Save();
        _mutex?.Dispose();
        base.OnExit(e);
    }

    private async Task InitializeAsync()
    {
        try
        {
            _mainViewModel.Busy = "Looking for Hearthstone…";
            _installDir = await Task.Run(() => HearthstoneInstall.FindInstallDirectory(_settings.HearthstonePath));
            if (_installDir is not null && _settings.HearthstonePath != _installDir)
            {
                _settings.HearthstonePath = _installDir;
                _settings.Save();
            }

            var locale = _settings.Locale == "auto" ? HearthstoneInstall.DetectLocale(_installDir) : _settings.Locale;
            CardArt.Locale = locale;

            var logConfigChanged = await Task.Run(() =>
            {
                try
                {
                    return LogConfig.Ensure();
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    Diag.Error("log.config not writable", ex);
                    return false;
                }
            });

            _mainViewModel.Busy = "Loading card data…";
            _cards = await CardDb.LoadAsync(locale, _http);
            if (_cards.Count == 0 && locale != "enUS")
            {
                _cards = await CardDb.LoadAsync("enUS", _http);
            }

            _session.Cards = _cards;
            _mainViewModel.SetCards(_cards);

            if (!_settings.HistoryImported)
            {
                await ImportLogsAsync(force: false);
            }

            if (_renderDir is not null)
            {
                await RenderUiAsync(_renderDir);
                return;
            }

            _watcher = new LogWatcher(_session, () => _installDir);
            _watcher.Start();
            _mainViewModel.Busy = string.Empty;

            if (logConfigChanged && HearthstoneInstall.IsRunning())
            {
                Notify("Restart Hearthstone", "Hearthlens enabled the game's logging. Restart Hearthstone once so tracking can start.");
            }

            await LoadMetaAsync();
            _metaRefresh.Start();
        }
        catch (Exception ex)
        {
            Diag.Error("Startup failed", ex);
            _mainViewModel.Busy = "Startup problem — see hearthlens.log in the data folder.";
        }
    }

    private async Task LoadMetaAsync()
    {
        try
        {
            var meta = await BattlegroundsMeta.LoadAsync(_http);
            if (meta is not null)
            {
                _meta = meta;
                _session.SetMeta(meta);
                _mainViewModel.SetMeta(meta);
            }
        }
        catch (Exception ex)
        {
            Diag.Error("Community stats unavailable", ex);
        }
    }

    private async Task ImportLogsAsync(bool force)
    {
        if (_installDir is null)
        {
            _mainViewModel.Busy = "Hearthstone install not found — nothing to import.";
            return;
        }

        _mainViewModel.Busy = "Importing past games from Hearthstone logs…";
        var progress = new Progress<string>(text => _mainViewModel.Busy = text);
        var result = await Task.Run(() => LogBackfill.Run(_installDir, _history, _cards, progress));
        _settings.HistoryImported = true;
        _settings.Save();
        _mainViewModel.Busy = force || result.GamesAdded > 0
            ? $"Imported {result.GamesAdded} new games from {result.Sessions} log sessions."
            : string.Empty;
        await _mainViewModel.RefreshAsync();
    }

    // ---- live snapshots ---------------------------------------------------------------------

    private void OnSnapshot(LiveSnapshot snapshot)
    {
        // Called on the log thread; coalesce into one UI update per dispatcher pass.
        Volatile.Write(ref _pending, snapshot);
        if (Interlocked.Exchange(ref _applyScheduled, 1) == 0)
        {
            Dispatcher.BeginInvoke(ApplySnapshot, DispatcherPriority.Background);
        }
    }

    private void ApplySnapshot()
    {
        Interlocked.Exchange(ref _applyScheduled, 0);
        var snapshot = Interlocked.Exchange(ref _pending, null);
        if (snapshot is null)
        {
            return;
        }

        _last = snapshot;
        _mainViewModel.Apply(snapshot);
        RefreshDeckStats(snapshot.Constructed);
        _overlayViewModel.Update(snapshot, _cards, _settings, _deckStats, _matchupStats);
    }

    private void RefreshDeckStats(ConstructedView? view)
    {
        var key = view is null ? null : $"{view.DeckName}|{view.PlayerClass}|{view.OpponentClass}";
        if (key == _deckStatsKey)
        {
            return;
        }

        _deckStatsKey = key;
        _deckStats = null;
        _matchupStats = null;
        if (view is null)
        {
            return;
        }

        _ = Task.Run(() =>
        {
            var deck = _history.DeckStats().FirstOrDefault(d => d.DeckName == view.DeckName && d.PlayerClass == view.PlayerClass);
            var matchup = _history.MatchupStats(deck?.DeckCode).FirstOrDefault(m => m.OpponentClass == view.OpponentClass);
            return (deck, matchup);
        }).ContinueWith(t =>
        {
            if (t.IsCompletedSuccessfully && _deckStatsKey == key)
            {
                (_deckStats, _matchupStats) = t.Result;
                _overlayViewModel.Update(_last, _cards, _settings, _deckStats, _matchupStats);
            }
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    private void OnGameFinished(GameOutcome outcome)
    {
        if (_settings.ShowGameToasts && _settings.OverlayEnabled)
        {
            _overlayViewModel.ShowOutcome(outcome);
        }
    }

    private void OnSettingsChanged()
    {
        _settingsDebounce.Stop();
        _settingsDebounce.Start();
        if (_trayOverlayItem is not null)
        {
            _trayOverlayItem.Checked = _settings.OverlayEnabled;
        }

        _overlayViewModel.Update(_last, _cards, _settings, _deckStats, _matchupStats);
        Autostart.Apply(_settings.StartWithWindows);
    }

    // ---- shell: tray + hotkey ----------------------------------------------------------------

    private void SetupTray()
    {
        using var iconStream = GetResourceStream(new Uri("pack://application:,,,/Assets/hearthlens.ico"))?.Stream;
        var menu = new Forms.ContextMenuStrip { Renderer = new DarkMenuRenderer(), ShowImageMargin = false, ShowCheckMargin = true };
        menu.Items.Add("Open Hearthlens", null, (_, _) => ShowMain());
        _trayOverlayItem = new Forms.ToolStripMenuItem("Overlay", null, (_, _) => ToggleOverlaySetting()) { Checked = _settings.OverlayEnabled };
        menu.Items.Add(_trayOverlayItem);
        menu.Items.Add("Arrange overlay panels", null, (_, _) => _overlayViewModel.LayoutEditing = !_overlayViewModel.LayoutEditing);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Quit", null, (_, _) => Quit());
        foreach (Forms.ToolStripItem item in menu.Items)
        {
            item.ForeColor = System.Drawing.Color.FromArgb(0xE7, 0xE9, 0xEE);
        }

        _tray = new Forms.NotifyIcon
        {
            Icon = iconStream is null ? System.Drawing.SystemIcons.Application : new System.Drawing.Icon(iconStream),
            Text = "Hearthlens",
            Visible = true,
            ContextMenuStrip = menu,
        };
        _tray.DoubleClick += (_, _) => ShowMain();
    }

    private void Notify(string title, string text)
    {
        _tray?.ShowBalloonTip(6000, title, text, Forms.ToolTipIcon.Info);
    }

    private void ShowMain()
    {
        _main.Show();
        if (_main.WindowState == WindowState.Minimized)
        {
            _main.WindowState = WindowState.Normal;
        }

        _main.Activate();
    }

    private void ToggleOverlaySetting()
    {
        _settings.OverlayEnabled = !_settings.OverlayEnabled;
        _main.SyncOverlaySwitch();
        OnSettingsChanged();
    }

    private void Quit()
    {
        _main.CloseForReal();
        _overlay.Close();
        Shutdown();
    }

    private void RegisterHotkey()
    {
        var handle = new WindowInteropHelper(_main).EnsureHandle();
        _hotkeySource = HwndSource.FromHwnd(handle);
        _hotkeySource?.AddHook((IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) =>
        {
            if (msg == Native.WM_HOTKEY && wParam.ToInt32() == HotkeyId)
            {
                _overlay.Suppressed = !_overlay.Suppressed;
                handled = true;
            }
            else if (msg == Native.WM_HOTKEY && wParam.ToInt32() == LayoutHotkeyId)
            {
                _overlayViewModel.LayoutEditing = !_overlayViewModel.LayoutEditing;
                handled = true;
            }

            return IntPtr.Zero;
        });
        if (!Native.RegisterHotKey(handle, HotkeyId, Native.MOD_CONTROL | Native.MOD_SHIFT | Native.MOD_NOREPEAT, 0x48))
        {
            Diag.Info("Ctrl+Shift+H is taken by another app; overlay hotkey disabled.");
        }

        if (!Native.RegisterHotKey(handle, LayoutHotkeyId, Native.MOD_CONTROL | Native.MOD_SHIFT | Native.MOD_NOREPEAT, 0x4C))
        {
            Diag.Info("Ctrl+Shift+L is taken by another app; layout hotkey disabled.");
        }
    }

    private DispatcherTimer Debouncer(TimeSpan delay, Action action)
    {
        var timer = new DispatcherTimer { Interval = delay };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            action();
        };
        return timer;
    }

    /// <summary>Tray menu in the app's dark palette.</summary>
    private sealed class DarkMenuRenderer : Forms.ToolStripProfessionalRenderer
    {
        private static readonly System.Drawing.Color Background = System.Drawing.Color.FromArgb(0x1C, 0x21, 0x29);
        private static readonly System.Drawing.Color Hover = System.Drawing.Color.FromArgb(0x2A, 0x31, 0x3C);
        private static readonly System.Drawing.Color Line = System.Drawing.Color.FromArgb(0x26, 0x2C, 0x36);

        protected override void OnRenderToolStripBackground(Forms.ToolStripRenderEventArgs e) => e.Graphics.Clear(Background);

        protected override void OnRenderToolStripBorder(Forms.ToolStripRenderEventArgs e)
        {
            using var pen = new System.Drawing.Pen(Line);
            e.Graphics.DrawRectangle(pen, 0, 0, e.AffectedBounds.Width - 1, e.AffectedBounds.Height - 1);
        }

        protected override void OnRenderMenuItemBackground(Forms.ToolStripItemRenderEventArgs e)
        {
            using var brush = new System.Drawing.SolidBrush(e.Item.Selected ? Hover : Background);
            e.Graphics.FillRectangle(brush, new System.Drawing.Rectangle(System.Drawing.Point.Empty, e.Item.Size));
        }

        protected override void OnRenderSeparator(Forms.ToolStripSeparatorRenderEventArgs e)
        {
            using var pen = new System.Drawing.Pen(Line);
            var y = e.Item.Height / 2;
            e.Graphics.DrawLine(pen, 4, y, e.Item.Width - 4, y);
        }

        protected override void OnRenderItemCheck(Forms.ToolStripItemImageRenderEventArgs e)
        {
            using var brush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(0xE2, 0xB5, 0x4C));
            e.Graphics.FillEllipse(brush, e.ImageRectangle.X + 4, e.ImageRectangle.Y + 4, 8, 8);
        }
    }
}

