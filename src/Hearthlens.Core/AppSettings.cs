using System.Text.Json;

namespace Hearthlens.Core;

public sealed class AppSettings
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    /// <summary>"auto" follows the installed client language.</summary>
    public string Locale { get; set; } = "auto";

    public string? HearthstonePath { get; set; }

    public double OverlayScale { get; set; } = 1.0;

    public double OverlayOpacity { get; set; } = 1.0;

    public bool OverlayEnabled { get; set; } = true;

    public bool ShowDeckTracker { get; set; } = true;

    public bool ShowOpponentTracker { get; set; } = true;

    public bool ShowDrawChance { get; set; } = true;

    public bool ShowBattlegroundsBar { get; set; } = true;

    public bool ShowOpponentBoards { get; set; } = true;

    public bool ShowHeroPick { get; set; } = true;

    public bool ShowGameToasts { get; set; } = true;

    public bool ShowLobbyPanel { get; set; } = true;

    public bool ShowSessionPanel { get; set; } = true;

    /// <summary>Show simulated win / tie / loss odds while a Battlegrounds combat plays out.</summary>
    public bool ShowCombatOdds { get; set; } = true;

    /// <summary>User-placed overlay panels by id; panels without an entry use their default spot.</summary>
    public Dictionary<string, PanelPlacement> Panels { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Fallback leaderboard hover areas, as fractions of the client height (the leaderboard scales with height).
    /// X is measured from the left edge of the 4:3 game area, where Hearthstone draws the leaderboard.
    /// Defaults match Hearthstone's layout; the live hover normally comes from the game's memory instead.
    /// </summary>
    public double LeaderboardTop { get; set; } = DefaultLeaderboardTop;

    public double LeaderboardSlotHeight { get; set; } = DefaultLeaderboardSlotHeight;

    public double LeaderboardWidth { get; set; } = DefaultLeaderboardWidth;

    /// <summary>Bumped when the meaning of the leaderboard fractions changes, so stale calibrations are reset.
    /// Absent from older settings files, so it defaults to 0 and <see cref="Migrate"/> brings it up to date.</summary>
    public int LeaderboardGeometryVersion { get; set; }

    public const double DefaultLeaderboardTop = 0.15;
    public const double DefaultLeaderboardSlotHeight = 0.69 / 8;
    public const double DefaultLeaderboardWidth = 0.10;
    private const int CurrentLeaderboardGeometry = 2;

    public bool ShowLeaderboardGuides { get; set; }

    public bool MinimizeToTray { get; set; } = true;

    public bool StartMinimized { get; set; }

    public bool StartWithWindows { get; set; }

    public bool HistoryImported { get; set; }

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(AppPaths.Settings))
            {
                var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(AppPaths.Settings), Json) ?? new AppSettings();
                settings.Migrate();
                return settings;
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            Diag.Error("Settings unreadable, using defaults", ex);
        }

        var fresh = new AppSettings();
        fresh.Migrate();
        return fresh;
    }

    private void Migrate()
    {
        // Version 1 measured X from the window edge, which misses the leaderboard on wide screens.
        if (LeaderboardGeometryVersion < CurrentLeaderboardGeometry)
        {
            LeaderboardTop = DefaultLeaderboardTop;
            LeaderboardSlotHeight = DefaultLeaderboardSlotHeight;
            LeaderboardWidth = DefaultLeaderboardWidth;
            LeaderboardGeometryVersion = CurrentLeaderboardGeometry;
        }
    }

    public void Save()
    {
        try
        {
            var temp = AppPaths.Settings + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(this, Json));
            File.Move(temp, AppPaths.Settings, overwrite: true);
        }
        catch (IOException ex)
        {
            Diag.Error("Settings not saved", ex);
        }
    }
}

/// <summary>Top-left corner as fractions of the overlay surface, plus a per-panel size multiplier.</summary>
public sealed class PanelPlacement
{
    public double X { get; set; }

    public double Y { get; set; }

    public double Scale { get; set; } = 1.0;
}
