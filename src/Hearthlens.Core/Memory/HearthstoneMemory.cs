using System.Diagnostics;

namespace Hearthlens.Core.Memory;

public sealed record BattlegroundsRating(int Solo, int Duos);

/// <summary>
/// Live gold costs of the Battlegrounds game-mode buttons. Null when that button is not on screen;
/// the costs include hero-power and trinket discounts.
/// </summary>
public sealed record BattlegroundsButtonCosts(int? Freeze, int? Roll, int? Upgrade, int? DarkGift);

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
    private bool _attaching;
    private int _attachFailures;

    // MONO_TYPE_I4 / MONO_TYPE_U4: the element types of the int fields ratings are stored in.
    private const byte MonoInt32 = 0x08;
    private const byte MonoUInt32 = 0x09;

    // One scan reads a few thousand values; attaching walks the whole class table once.
    private const int ScanReadBudget = 50_000;
    private static readonly TimeSpan ScanTimeBudget = TimeSpan.FromSeconds(1.5);
    private const int AttachReadBudget = 3_000_000;
    private static readonly TimeSpan AttachTimeBudget = TimeSpan.FromSeconds(30);
    private DateTime _nextBudgetNote = DateTime.MinValue;
    private readonly Dictionary<string, ulong> _netCacheObjects = new(StringComparer.Ordinal);
    private DateTime _netCacheRefreshAt = DateTime.MinValue;

    // What this client build calls each field the reads below need, keyed by class address and role, plus the class
    // addresses themselves. A game update that renames or moves one costs a single lookup, not one per scan.
    private readonly Dictionary<(ulong Class, string Role), string?> _fieldNames = new();
    private readonly Dictionary<string, ulong> _classes = new(StringComparer.Ordinal);

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

            if (_attaching || DateTime.UtcNow < _nextAttachAttempt)
            {
                return false;
            }

            _attaching = true;
        }

        // Attaching reads the game's whole class table. It runs outside the lock, so IsAttached (asked on the log
        // thread for every snapshot) never waits for it.
        (ProcessMemory? Memory, MonoRuntime? Runtime, string Status, bool Found) result;
        try
        {
            result = Attach();
        }
        catch (Exception ex)
        {
            result = (null, null, "Attach failed: " + ex.Message, true);
        }

        lock (_gate)
        {
            _attaching = false;
            Status = result.Status;
            if (result.Runtime is null)
            {
                result.Memory?.Dispose();
                // A game that is running but cannot be read (an update moved things, a security tool blocks the
                // reads) is tried less and less often instead of every few seconds.
                _attachFailures = result.Found ? _attachFailures + 1 : 0;
                _nextAttachAttempt = DateTime.UtcNow.AddSeconds(Math.Clamp(5 * _attachFailures, 5, 60));
                return false;
            }

            _attachFailures = 0;
            _memory = result.Memory;
            _runtime = result.Runtime;
            return true;
        }
    }

    private static (ProcessMemory? Memory, MonoRuntime? Runtime, string Status, bool Found) Attach()
    {
        var processes = Process.GetProcessesByName("Hearthstone");
        try
        {
            if (processes.Length == 0)
            {
                return (null, null, "Hearthstone not running", false);
            }

            var memory = ProcessMemory.Open(processes[0].Id);
            if (memory is null)
            {
                return (null, null, "Cannot open Hearthstone process (run as the same user)", true);
            }

            using (ProcessMemory.Budget(AttachReadBudget, AttachTimeBudget))
            {
                var runtime = MonoRuntime.Attach(memory, out var error);
                if (runtime is null || ProcessMemory.BudgetExhausted)
                {
                    memory.Dispose();
                    return (null, null, runtime is null ? error : "Attaching took too long, retrying", true);
                }

                return (memory, runtime, $"Attached · {runtime.ClassCount:N0} classes", true);
            }
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
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

    // The NetCache object and fields the client used when this was written. A game update that renames them is
    // picked up by DiscoverRating, so a new patch does not need a new release just for the rating.
    private static readonly RatingLayout KnownRatingLayout =
        new("NetCacheBaconRatingInfo", "<Rating>k__BackingField", "<DuosRating>k__BackingField");

    private RatingLayout _ratingLayout = KnownRatingLayout;
    private DateTime _nextRatingDiscovery = DateTime.MinValue;
    private DateTime _nextRatingMissNote = DateTime.MinValue;

    /// <summary>A NetCache class plus the int fields holding the solo and, when the client reports one, the Duos rating.</summary>
    private sealed record RatingLayout(string ClassName, string SoloField, string? DuosField);

    public BattlegroundsRating? GetBattlegroundsRating() => Safe(rt =>
    {
        var layout = _ratingLayout;
        if (ReadRating(CachedNetCacheObject(rt, layout.ClassName), layout) is { } known)
        {
            return known;
        }

        // The layout that worked before no longer gives a rating: the client was updated, or the cached object is
        // not the one the game uses now. Look for it again, at most every 30 seconds.
        if (DateTime.UtcNow < _nextRatingDiscovery)
        {
            return null;
        }

        _nextRatingDiscovery = DateTime.UtcNow.AddSeconds(30);
        return DiscoverRating(rt);
    });

    private static BattlegroundsRating? ReadRating(MonoObject info, RatingLayout layout)
    {
        if (info.IsNull)
        {
            return null;
        }

        var solo = info.Int(layout.SoloField, -1);
        var duos = layout.DuosField is null ? -1 : info.Int(layout.DuosField, -1);
        return solo <= 0 && duos <= 0 ? null : new BattlegroundsRating(solo, duos);
    }

    /// <summary>
    /// Finds the rating without trusting this release's object or field names: every NetCache object that looks like a
    /// Battlegrounds rating cache is inspected and its int fields called "…Rating…" are read. The layout that is found
    /// is remembered (and logged), so a game update that renames things is handled without a code change.
    /// </summary>
    private BattlegroundsRating? DiscoverRating(MonoRuntime rt)
    {
        var netCache = FindService(rt, "NetCache")["m_netCache"];
        if (netCache.IsNull)
        {
            return null;
        }

        var candidates = netCache.MapValues().Where(o => o.ClassName is { } name && LooksLikeRatingCache(name)).ToList();
        if (candidates.Count == 0)
        {
            // No Battlegrounds cache at all (the client has not sent the rating yet, or the name changed further).
            if (DateTime.UtcNow >= _nextRatingMissNote)
            {
                _nextRatingMissNote = DateTime.UtcNow.AddMinutes(1);
                Diag.Info("Battlegrounds rating: the client's NetCache holds no Battlegrounds rating object yet");
            }

            return null;
        }

        BattlegroundsRating? best = null;
        var bestLayout = _ratingLayout;
        var bestObject = default(MonoObject);
        var bestSolo = 0;
        foreach (var candidate in candidates)
        {
            if (candidate.ClassName is not { } className || candidate.Class is not { } cls)
            {
                continue;
            }

            var fields = cls.AllFields()
                .Where(f => !f.IsStatic && f.TypeCode is MonoInt32 or MonoUInt32 && f.Name.Contains("Rating", StringComparison.OrdinalIgnoreCase))
                .ToList();
            var solo = fields
                .Where(f => !f.Name.Contains("Duo", StringComparison.OrdinalIgnoreCase))
                .Select(f => (Field: f.Name, Value: candidate.Int(f.Name, -1)))
                .Where(x => IsPlausibleRating(x.Value))
                .OrderByDescending(x => x.Value)
                .FirstOrDefault();
            if (solo.Field is null || solo.Value <= bestSolo)
            {
                continue;
            }

            // A Duos field is kept even while it reads 0: the client only fills it once that ladder is played.
            var duos = fields
                .Where(f => f.Name.Contains("Duo", StringComparison.OrdinalIgnoreCase))
                .Select(f => (Field: f.Name, Value: candidate.Int(f.Name, -1)))
                .FirstOrDefault(x => x.Value is >= 0 and < 100_000);
            bestSolo = solo.Value;
            bestLayout = new RatingLayout(className, solo.Field, duos.Field);
            bestObject = candidate;
            best = new BattlegroundsRating(solo.Value, duos.Field is null ? 0 : duos.Value);
        }

        if (best is null)
        {
            return null;
        }

        // Hold on to the object that actually carried the rating: the address the cache had before was the reason
        // the read stopped giving a value, so every later scan would otherwise rediscover it again.
        lock (_netCacheObjects)
        {
            _netCacheObjects[bestLayout.ClassName] = bestObject.Address;
        }

        if (bestLayout != _ratingLayout)
        {
            _ratingLayout = bestLayout;
            Diag.Info($"Battlegrounds rating read from {bestLayout.ClassName}.{bestLayout.SoloField}"
                      + (bestLayout.DuosField is null ? string.Empty : $" and {bestLayout.DuosField}"));
        }

        return best;
    }

    /// <summary>Only the Battlegrounds cache: the constructed ladder keeps its own, differently shaped rating caches.</summary>
    private static bool LooksLikeRatingCache(string className) =>
        className.Contains("Rating", StringComparison.OrdinalIgnoreCase)
        && (className.Contains("Bacon", StringComparison.OrdinalIgnoreCase)
            || className.Contains("Battlegrounds", StringComparison.OrdinalIgnoreCase));

    /// <summary>Battlegrounds ratings run from a few hundred to well over 10,000; 0 means "not reported".</summary>
    private static bool IsPlausibleRating(int value) => value is > 0 and < 100_000;

    /// <summary>
    /// The NetCache map holds every object the server sent. Walking it costs thousands of small reads and the map
    /// grows during a session, which is slow enough on a weak PC to stall the log reader. The address of the
    /// object we need is remembered and only looked up again every few minutes.
    /// </summary>
    private MonoObject CachedNetCacheObject(MonoRuntime rt, string className)
    {
        lock (_netCacheObjects)
        {
            return CachedNetCacheObjectCore(rt, className);
        }
    }

    private MonoObject CachedNetCacheObjectCore(MonoRuntime rt, string className)
    {
        var now = DateTime.UtcNow;
        if (now >= _netCacheRefreshAt)
        {
            _netCacheRefreshAt = now.AddMinutes(3);
            _netCacheObjects.Clear();
        }

        if (_netCacheObjects.TryGetValue(className, out var address) && address != 0)
        {
            var cached = rt.Object(address);
            if (cached.ClassName == className)
            {
                return cached;
            }
        }

        var found = FindNetCacheObject(rt, className);
        if (found.IsNull)
        {
            _netCacheObjects.Remove(className);
            return default;
        }

        _netCacheObjects[className] = found.Address;
        return found;
    }

    /// <summary>Rating shown on the post-game screen once the server has sent it, else null.</summary>
    public int? GetPostGameBattlegroundsRating() => Safe<int?>(rt =>
    {
        var gameEntity = GameStateInstance(rt)["m_gameEntity"];
        var change = gameEntity["<RatingChangeData>k__BackingField"];
        if (change.IsNull && gameEntity.Class is { } cls)
        {
            // The field was renamed: take whichever field on the game entity holds the rating change.
            var field = cls.AllFields().FirstOrDefault(f => !f.IsStatic
                && f.Name.Contains("RatingChange", StringComparison.OrdinalIgnoreCase));
            change = field is null ? default : gameEntity.Object(field.Name);
        }

        if (change.IsNull)
        {
            return null;
        }

        var value = change.Int("_NewRating", -1);
        return IsPlausibleRating(value) ? value : DiscoverRatingValue(change);
    });

    /// <summary>
    /// A rating stored in a field whose name this release does not know: the object holds one int that looks like a
    /// rating, preferring a field called "…New…" (the rating after the game) over any other.
    /// </summary>
    private static int? DiscoverRatingValue(MonoObject obj)
    {
        if (obj.Class is not { } cls)
        {
            return null;
        }

        return cls.AllFields()
            .Where(f => !f.IsStatic && f.TypeCode is MonoInt32 or MonoUInt32)
            .Select(f => (f.Name, Value: obj.Int(f.Name, -1)))
            .Where(x => IsPlausibleRating(x.Value))
            .OrderByDescending(x => x.Name.Contains("New", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
            .ThenByDescending(x => x.Value)
            .Select(x => (int?)x.Value)
            .FirstOrDefault();
    }

    /// <summary>TAG_RACE values of the tribes in the current Battlegrounds lobby.</summary>
    public IReadOnlyList<int>? GetBattlegroundsRaces() => Safe(rt =>
    {
        var state = GameStateInstance(rt);
        var field = Field(state.Class, "races", ["m_availableRacesInBattlegroundsExcludingAmalgam"],
            ["availableRaces", "RacesInBattlegrounds", "Races"]);
        var list = field is null ? default : state.Object(field);
        if (list.IsNull)
        {
            return null;
        }

        var races = list.IntListItems();
        return races.Count is > 0 and < 16 ? races : null;
    });

    /// <summary>
    /// Live gold costs of the Battlegrounds game-mode buttons: freeze (slot 1), reroll (slot 2),
    /// tavern upgrade (slot 3) and Dark Gift (slot 5). Null when the button is not on screen.
    /// </summary>
    public BattlegroundsButtonCosts? GetBattlegroundsButtonCosts() => Safe<BattlegroundsButtonCosts?>(rt =>
    {
        var zoneMgr = Instance(rt, "ZoneMgr", "ZoneMgr");
        var zonesField = Field(zoneMgr.Class, "zones", ["m_zones"], ["zones"]);
        if (zoneMgr.IsNull || zonesField is null)
        {
            return null;
        }

        int? freeze = null, roll = null, upgrade = null, darkGift = null;
        foreach (var zone in zoneMgr.Object(zonesField).ListItems())
        {
            var zoneClass = zone.Class;
            if (zoneClass is null || !zoneClass.Name.Contains("GameModeButton", StringComparison.Ordinal))
            {
                continue;
            }

            var sideField = Field(zoneClass, "side", ["m_Side"], ["Side"], integer: true);
            var slotField = Field(zoneClass, "slot", ["m_ButtonSlot"], ["ButtonSlot", "Slot"], integer: true);
            var cardsField = Field(zoneClass, "cards", ["m_cards"], ["cards"]);
            if (sideField is null || slotField is null || cardsField is null || zone.Int(sideField) != 1)
            {
                continue;
            }

            var slot = zone.Int(slotField);
            if (slot is not (1 or 2 or 3 or 5))
            {
                continue;
            }

            foreach (var card in zone.Object(cardsField).ListItems())
            {
                var entityField = Field(card.Class, "entity", ["m_entity"], ["entity"]);
                var entity = entityField is null ? default : card.Object(entityField);
                if (entity.IsNull)
                {
                    continue;
                }

                var cardIdField = Field(entity.Class, "cardId", ["m_cardIdInternal"], ["cardId"]);
                var cardId = cardIdField is null ? null : entity.String(cardIdField);
                if (cardId is null)
                {
                    continue;
                }

                // Slot 3 is the upgrade only when the card is the tier button; other slots are stable enough
                // to read by position (freeze, reroll, Dark Gift).
                if (slot == 3 && !cardId.StartsWith("TB_BaconShopTechUp", StringComparison.Ordinal))
                {
                    continue;
                }

                var costField = Field(entity.Class, "cost", ["m_realTimeCost"], ["realTimeCost", "Cost"], integer: true);
                var cost = costField is null ? 0 : entity.Int(costField);
                switch (slot)
                {
                    case 1: freeze = cost; break;
                    case 2: roll = cost; break;
                    case 3: upgrade = cost; break;
                    case 5: darkGift = cost; break;
                }

                break;
            }
        }

        return new BattlegroundsButtonCosts(freeze, roll, upgrade, darkGift);
    });

    /// <summary>Gold cost of the live tavern-upgrade button, or null when it is not on screen.</summary>
    public int? GetTavernUpgradeCost() => GetBattlegroundsButtonCosts()?.Upgrade;

    /// <summary>
    /// Card id of the hero whose Battlegrounds leaderboard tile is under the mouse, or null. Read straight from the
    /// game's leaderboard so it works at any resolution or aspect ratio.
    /// </summary>
    public string? GetHoveredLeaderboardHero() => Safe(rt =>
    {
        var manager = Instance(rt, "PlayerLeaderboardManager", "PlayerLeaderboardManager");
        var hovered = Field(manager.Class, "leaderboardHovered", ["m_isMousedOver"], ["isMousedOver", "MousedOver"]);
        if (manager.IsNull || hovered is null || !manager.Bool(hovered))
        {
            return null;
        }

        var tileField = Field(manager.Class, "hoveredTile", ["m_currentlyMousedOverTile"], ["MousedOverTile", "Tile"]);
        var tile = tileField is null ? default : manager.Object(tileField);
        if (tile.IsNull)
        {
            return null;
        }

        var heroField = Field(tile.Class, "heroEntity", ["m_playerHeroEntity"], ["playerHeroEntity"])
                        ?? Field(tile.Class, "entity", ["m_entity"], ["entity"]);
        var hero = heroField is null ? default : tile.Object(heroField);
        var cardIdField = Field(hero.Class, "cardId", ["m_cardIdInternal"], ["cardId"]);
        return hero.IsNull || cardIdField is null ? null : hero.String(cardIdField);
    });

    /// <summary>Ranked medals for both players of the current game (only valid in-game).</summary>
    public (MatchPlayerInfo? Local, MatchPlayerInfo? Opponent)? GetMatchPlayers() => Safe<(MatchPlayerInfo?, MatchPlayerInfo?)?>(rt =>
    {
        var state = GameStateInstance(rt);
        var mapField = Field(state.Class, "playerMap", ["m_playerMap"], ["playerMap"]);
        var playerMap = mapField is null ? default : state.Object(mapField);
        if (playerMap.IsNull)
        {
            return null;
        }

        MatchPlayerInfo? local = null, opponent = null;
        foreach (var player in playerMap.MapValues())
        {
            var playerClass = player.Class;
            if (playerClass is null || !playerClass.Name.Contains("Player", StringComparison.Ordinal))
            {
                continue;
            }

            var sideField = Field(playerClass, "side", ["m_side"], ["side"], integer: true);
            var info = ReadPlayer(player);
            switch (sideField is null ? -1 : player.Int(sideField))
            {
                case 1: local = info; break;
                case 2: opponent = info; break;
            }
        }

        return local is null && opponent is null ? null : (local, opponent);
    });

    private MatchPlayerInfo ReadPlayer(MonoObject player)
    {
        RankInfo? standard = null, wild = null, classic = null, twist = null;
        var medalInfoField = Field(player.Class, "medalInfo", ["m_medalInfo"], ["MedalInfo"]);
        var medalInfo = medalInfoField is null ? default : player.Object(medalInfoField);
        var medalsField = Field(medalInfo.Class, "currentMedals", ["m_currMedalInfo"], ["currMedal", "Medal"]);
        var medals = medalsField is null ? default : medalInfo.Object(medalsField);
        foreach (var medal in medals.MapValues())
        {
            var medalClass = medal.Class;
            var formatField = Field(medalClass, "format", ["format"], ["format"], integer: true);
            var leagueField = Field(medalClass, "league", ["leagueId"], ["league"], integer: true);
            var starField = Field(medalClass, "stars", ["starLevel"], ["star"], integer: true);
            var legendField = Field(medalClass, "legend", ["legendIndex"], ["legend"], integer: true);
            var seasonField = Field(medalClass, "season", ["seasonId"], ["season"], integer: true);
            var format = formatField is null ? 0 : medal.Int(formatField);
            var rank = new RankInfo(
                FormatName(format),
                leagueField is null ? 0 : medal.Int(leagueField),
                starField is null ? 0 : medal.Int(starField),
                legendField is null ? 0 : medal.Int(legendField),
                seasonField is null ? 0 : medal.Int(seasonField));
            switch (format)
            {
                case 1: wild = rank; break;
                case 2: standard = rank; break;
                case 3: classic = rank; break;
                case 4: twist = rank; break;
            }
        }

        var nameField = Field(player.Class, "name", ["m_name"], ["name"]);
        return new MatchPlayerInfo(nameField is null ? null : player.String(nameField), standard, wild, classic, twist);
    }

    private static string FormatName(int format) => format switch
    {
        1 => "Wild",
        2 => "Standard",
        3 => "Classic",
        4 => "Twist",
        _ => "Unknown",
    };

    private MonoObject GameStateInstance(MonoRuntime rt) => Instance(rt, "GameState", "GameState");

    /// <summary>
    /// The client's singleton for a class (GameState.s_instance, ZoneMgr.s_instance, …). Both the class and the static
    /// field that holds the instance are looked up by name, so a renamed field or a class that gained a namespace is
    /// found without a code change.
    /// </summary>
    private MonoObject Instance(MonoRuntime rt, string role, params string[] classNames)
    {
        var cls = Class(rt, role, classNames);
        var field = Field(cls, role + ".instance", ["s_instance"], ["instance"]);
        return cls is null || field is null ? default : cls.GetStatic(field);
    }

    /// <summary>A class by name, else by the tail of its full name (a namespace added in a game update).</summary>
    private MonoClass? Class(MonoRuntime rt, string role, string[] names)
    {
        if (_classes.TryGetValue(role, out var address) && address != 0 && rt.ClassAt(address) is { } cached)
        {
            return cached;
        }

        var found = names.Select(rt.FindClass).FirstOrDefault(c => c is not null);
        found ??= rt.ClassNames
            .Where(n => names.Any(name => n.EndsWith("." + name, StringComparison.Ordinal)
                                          || n.EndsWith("+" + name, StringComparison.Ordinal)))
            .Select(rt.FindClass)
            .FirstOrDefault(c => c is not null);
        if (found is not null)
        {
            _classes[role] = found.Address;
        }

        return found;
    }

    /// <summary>
    /// The name this client build uses for a field: the known names first, then the first field whose name contains
    /// one of the hints (in order). The answer is remembered per class and dropped when the game exits, so the field
    /// hunt runs once per game update instead of once per scan.
    /// </summary>
    private string? Field(MonoClass? cls, string role, string[] known, string[] hints, bool integer = false)
    {
        if (cls is null)
        {
            return null;
        }

        var key = (cls.Address, role);
        lock (_fieldNames)
        {
            if (_fieldNames.TryGetValue(key, out var remembered))
            {
                return remembered;
            }
        }

        var found = known.FirstOrDefault(name => cls.FindField(name) is not null);
        if (found is null)
        {
            foreach (var hint in hints)
            {
                found = cls.AllFields()
                    .FirstOrDefault(f => !f.IsStatic
                        && (!integer || f.TypeCode is MonoInt32 or MonoUInt32)
                        && f.Name.Contains(hint, StringComparison.OrdinalIgnoreCase))?.Name;
                if (found is not null)
                {
                    break;
                }
            }
        }

        // A field list cut short by the read budget must not hide the field for good.
        if (!ProcessMemory.BudgetExhausted)
        {
            lock (_fieldNames)
            {
                _fieldNames[key] = found;
            }
        }

        return found;
    }

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
            using (ProcessMemory.Budget(ScanReadBudget, ScanTimeBudget))
            {
                var result = read(runtime);
                if (!ProcessMemory.BudgetExhausted)
                {
                    return result;
                }
            }

            // The scan was cut short (usually objects freed mid-scan): its half-read values are not used.
            if (DateTime.UtcNow >= _nextBudgetNote)
            {
                _nextBudgetNote = DateTime.UtcNow.AddMinutes(1);
                Diag.Info("A memory scan ran out of its read budget and was skipped");
            }

            return default;
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
        _netCacheRefreshAt = DateTime.MinValue;
        // A new game process may be a new client build: find the rating layout again instead of reusing the old one.
        _ratingLayout = KnownRatingLayout;
        _nextRatingDiscovery = DateTime.MinValue;
        lock (_fieldNames)
        {
            _fieldNames.Clear();
        }

        _classes.Clear();
    }

    public void Dispose()
    {
        lock (_gate)
        {
            DetachLocked();
        }
    }
}
