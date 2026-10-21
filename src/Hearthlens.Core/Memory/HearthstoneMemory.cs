using System.Diagnostics;

namespace Hearthlens.Core.Memory;

public sealed record BattlegroundsRating(int Solo, int Duos);

public sealed record RankInfo(string Format, int LeagueId, int StarLevel, int LegendRank, int SeasonId)
{
    private static readonly string[] Leagues = ["Bronze", "Silver", "Gold", "Platinum", "Diamond"];

    public bool IsLegend => LegendRank > 0 || StarLevel >= 51;

    /// <summary>Human readable rank, e.g. "Diamond 4" or "Legend 1,204".</summary>
    public string Display
    {
        get
        {
            if (IsLegend)
            {
                return LegendRank > 0 ? $"Legend {LegendRank:N0}" : "Legend";
            }

            if (StarLevel <= 0)
            {
                return "Unranked";
            }

            var league = Leagues[Math.Clamp((StarLevel - 1) / 10, 0, Leagues.Length - 1)];
            var rank = 10 - ((StarLevel - 1) % 10);
            return $"{league} {rank}";
        }
    }
}

public sealed record MatchPlayerInfo(string? Name, RankInfo? Standard, RankInfo? Wild, RankInfo? Classic, RankInfo? Twist);

/// <summary>
/// Reads Hearthstone client state straight from process memory: Battlegrounds rating, ranked medals,
/// the Battlegrounds tribe pool. All calls are read-only and return null when data is unavailable.
/// </summary>
public sealed class HearthstoneMemory : IDisposable
{
    private readonly object _gate = new();
    private ProcessMemory? _memory;
    private MonoRuntime? _runtime;
    private DateTime _nextAttachAttempt = DateTime.MinValue;

    public bool IsAttached
    {
        get
        {
            lock (_gate)
            {
                return _runtime is not null && _memory is { HasExited: false };
            }
        }
    }

    /// <summary>
    /// Reading the client's memory (rating, ranks, lobby tribes) uses Windows APIs. On other platforms everything
    /// else keeps working from the logs; these values are simply unavailable.
    /// </summary>
    public static bool IsSupported => OperatingSystem.IsWindows();

    public string Status { get; private set; } = IsSupported ? "Waiting for Hearthstone" : "Memory reading is only available on Windows";

    /// <summary>Attaches to a running Hearthstone process; cheap to call repeatedly.</summary>
    public bool EnsureAttached()
    {
        lock (_gate)
        {
            if (_runtime is not null && _memory is { HasExited: false })
            {
                return true;
            }

            DetachLocked();
            if (!IsSupported)
            {
                Status = "Memory reading is only available on Windows";
                return false;
            }

            if (DateTime.UtcNow < _nextAttachAttempt)
            {
                return false;
            }

            _nextAttachAttempt = DateTime.UtcNow.AddSeconds(5);
            var process = Process.GetProcessesByName("Hearthstone").FirstOrDefault();
            if (process is null)
            {
                Status = "Hearthstone not running";
                return false;
            }

            using (process)
            {
                var memory = ProcessMemory.Open(process.Id);
                if (memory is null)
                {
                    Status = "Cannot open Hearthstone process (run as the same user)";
                    return false;
                }

                var runtime = MonoRuntime.Attach(memory, out var error);
                if (runtime is null)
                {
                    memory.Dispose();
                    Status = error;
                    return false;
                }

                _memory = memory;
                _runtime = runtime;
                Status = $"Attached · {runtime.ClassCount:N0} classes";
                return true;
            }
        }
    }

    public MonoRuntime? Runtime
    {
        get
        {
            lock (_gate)
            {
                return _runtime;
            }
        }
    }

    public BattlegroundsRating? GetBattlegroundsRating() => Safe(rt =>
    {
        var info = FindNetCacheObject(rt, "NetCacheBaconRatingInfo");
        if (info.IsNull)
        {
            return null;
        }

        var solo = info.Int("<Rating>k__BackingField", -1);
        var duos = info.Int("<DuosRating>k__BackingField", -1);
        return solo <= 0 && duos <= 0 ? null : new BattlegroundsRating(solo, duos);
    });

    /// <summary>Rating shown on the post-game screen once the server has sent it, else null.</summary>
    public int? GetPostGameBattlegroundsRating() => Safe<int?>(rt =>
    {
        var gameEntity = GameStateInstance(rt)["m_gameEntity"];
        var change = gameEntity["<RatingChangeData>k__BackingField"];
        var value = change.Int("_NewRating", -1);
        return value > 0 ? value : null;
    });

    /// <summary>TAG_RACE values of the tribes in the current Battlegrounds lobby.</summary>
    public IReadOnlyList<int>? GetBattlegroundsRaces() => Safe(rt =>
    {
        var list = GameStateInstance(rt)["m_availableRacesInBattlegroundsExcludingAmalgam"];
        if (list.IsNull)
        {
            return null;
        }

        var races = list.IntListItems();
        return races.Count is > 0 and < 16 ? races : null;
    });

    /// <summary>
    /// Card id of the hero whose Battlegrounds leaderboard tile is under the mouse, or null. Read straight from the
    /// game's leaderboard so it works at any resolution or aspect ratio.
    /// </summary>
    public string? GetHoveredLeaderboardHero() => Safe(rt =>
    {
        var manager = rt.FindClass("PlayerLeaderboardManager")?.GetStatic("s_instance") ?? default;
        if (manager.IsNull || !manager.Bool("m_isMousedOver"))
        {
            return null;
        }

        var tile = manager["m_currentlyMousedOverTile"];
        if (tile.IsNull)
        {
            return null;
        }

        var hero = tile["m_playerHeroEntity"];
        if (hero.IsNull)
        {
            hero = tile["m_entity"];
        }

        return hero.IsNull ? null : hero.String("m_cardIdInternal");
    });

    /// <summary>Ranked medals for both players of the current game (only valid in-game).</summary>
    public (MatchPlayerInfo? Local, MatchPlayerInfo? Opponent)? GetMatchPlayers() => Safe<(MatchPlayerInfo?, MatchPlayerInfo?)?>(rt =>
    {
        var playerMap = GameStateInstance(rt)["m_playerMap"];
        if (playerMap.IsNull)
        {
            return null;
        }

        MatchPlayerInfo? local = null, opponent = null;
        foreach (var player in playerMap.MapValues())
        {
            if (player.ClassName != "Player")
            {
                continue;
            }

            var side = player.Int("m_side");
            var info = ReadPlayer(player);
            if (side == 1) local = info;
            else if (side == 2) opponent = info;
        }

        return local is null && opponent is null ? null : (local, opponent);
    });

    private static MatchPlayerInfo ReadPlayer(MonoObject player)
    {
        RankInfo? standard = null, wild = null, classic = null, twist = null;
        var medals = player["m_medalInfo"]["m_currMedalInfo"];
        foreach (var medal in medals.MapValues())
        {
            var format = medal.Int("format");
            var rank = new RankInfo(
                FormatName(format),
                medal.Int("leagueId"),
                medal.Int("starLevel"),
                medal.Int("legendIndex"),
                medal.Int("seasonId"));
            switch (format)
            {
                case 1: wild = rank; break;
                case 2: standard = rank; break;
                case 3: classic = rank; break;
                case 4: twist = rank; break;
            }
        }

        return new MatchPlayerInfo(player.String("m_name"), standard, wild, classic, twist);
    }

    private static string FormatName(int format) => format switch
    {
        1 => "Wild",
        2 => "Standard",
        3 => "Classic",
        4 => "Twist",
        _ => "Unknown",
    };

    private static MonoObject GameStateInstance(MonoRuntime rt) => rt.FindClass("GameState")?.GetStatic("s_instance") ?? default;

    /// <summary>Resolves a service registered in Hearthstone's service locator by type name.</summary>
    public static MonoObject FindService(MonoRuntime rt, string serviceName)
    {
        var jobs = rt.FindClass("Hearthstone.HearthstoneJobs");
        var builders = jobs?.GetStatic("s_dependencyBuilder") ?? default;
        foreach (var builder in builders.ListItems())
        {
            var services = builder["m_serviceLocator"]["m_services"];
            foreach (var entry in services.DictionaryValues())
            {
                if (entry.String("<ServiceTypeName>k__BackingField") == serviceName)
                {
                    return entry["<Service>k__BackingField"];
                }
            }
        }

        return default;
    }

    public static MonoObject FindNetCacheObject(MonoRuntime rt, string className)
    {
        var netCache = FindService(rt, "NetCache")["m_netCache"];
        foreach (var value in netCache.MapValues())
        {
            if (value.ClassName == className)
            {
                return value;
            }
        }

        return default;
    }

    private T? Safe<T>(Func<MonoRuntime, T?> read)
    {
        if (!EnsureAttached())
        {
            return default;
        }

        MonoRuntime? runtime;
        lock (_gate)
        {
            runtime = _runtime;
        }

        if (runtime is null)
        {
            return default;
        }

        try
        {
            return read(runtime);
        }
        catch (Exception ex)
        {
            Status = "Read failed: " + ex.Message;
            return default;
        }
    }

    private void DetachLocked()
    {
        _runtime = null;
        _memory?.Dispose();
        _memory = null;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            DetachLocked();
        }
    }
}
