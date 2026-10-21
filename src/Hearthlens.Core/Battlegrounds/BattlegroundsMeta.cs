using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace Hearthlens.Core.Battlegrounds;

public sealed record MetaTribeImpact(int Tribe, double Impact);

public sealed record MetaHero(
    string HeroCardId,
    int DataPoints,
    double AveragePosition,
    double PickRate,
    IReadOnlyDictionary<int, double> TribeImpact);

public sealed record MetaTrinket(
    string CardId,
    int DataPoints,
    double AveragePlacement,
    double PickRate,
    IReadOnlyDictionary<int, (double Placement, int DataPoints)> PlacementAtMmr);

public sealed record MetaCompCard(string CardId, string Status);

/// <summary>
/// Community results for one card (minion or tavern spell): the average final placement of players who
/// played it, overall and per turn it was played.
/// </summary>
public sealed record MetaCard(
    string CardId,
    int DataPoints,
    double AveragePlacement,
    IReadOnlyDictionary<int, (int Played, double Placement)> Turns);

public sealed record MetaComp(
    string Id,
    string Name,
    string PowerLevel,
    string Difficulty,
    IReadOnlyList<string> Tribes,
    IReadOnlyList<MetaCompCard> Cards,
    string? WhenToCommit,
    string? Tip,
    double AveragePlacement,
    IReadOnlyDictionary<int, double> PlacementAtMmr,
    IReadOnlyDictionary<string, (int DataPoints, double Placement)> HeroPlacement);

/// <summary>
/// Community Battlegrounds statistics (hero placement per MMR bracket and tribe, comp archetypes)
/// from the public data files published by Firestone (zerotoheroes.com). Cached on disk.
/// </summary>
public sealed class BattlegroundsMeta
{
    public const string Attribution = "Community stats: Firestone (zerotoheroes.com)";

    private static readonly int[] Brackets = [100, 50, 25, 10, 1];
    private static readonly TimeSpan MaxAge = TimeSpan.FromHours(6);
    private const string Root = "https://static.zerotoheroes.com/";

    private static readonly Dictionary<string, string> PrefixTribes = new(StringComparer.Ordinal)
    {
        ["beast"] = "BEAST", ["dragon"] = "DRAGON", ["aberration"] = "ABERRATION", ["abberation"] = "ABERRATION",
        ["murloc"] = "MURLOC", ["demon"] = "DEMON", ["quilboar"] = "QUILBOAR", ["mech"] = "MECHANICAL",
        ["pirate"] = "PIRATE", ["elemental"] = "ELEMENTAL", ["undead"] = "UNDEAD", ["naga"] = "NAGA",
    };

    private BattlegroundsMeta(
        IReadOnlyDictionary<int, IReadOnlyDictionary<string, MetaHero>> heroes,
        IReadOnlyList<(int Percentile, int Mmr)> percentiles,
        IReadOnlyList<MetaComp> comps,
        IReadOnlyDictionary<string, MetaTrinket> trinkets,
        IReadOnlyDictionary<int, IReadOnlyDictionary<string, MetaCard>> cards,
        DateTime updated)
    {
        HeroesByBracket = heroes;
        Percentiles = percentiles;
        Comps = comps;
        Trinkets = trinkets;
        CardsByBracket = cards;
        Updated = updated;
    }

    /// <summary>Minimum plays before a card's placement is trusted.</summary>
    public const int MinCardGames = 200;

    /// <summary>Card results per MMR bracket (100 = all ranks).</summary>
    public IReadOnlyDictionary<int, IReadOnlyDictionary<string, MetaCard>> CardsByBracket { get; }

    /// <summary>All-ranks results for a card, or null.</summary>
    public MetaCard? Card(string cardId) =>
        CardsByBracket.TryGetValue(100, out var all) ? all.GetValueOrDefault(cardId) : null;

    /// <summary>
    /// Average final placement of players who played the card around <paramref name="turn"/>. Nearby turns are
    /// pooled until the sample reaches <see cref="MinCardGames"/>; a thin bracket falls back to all ranks.
    /// Comparing cards at the same turn removes most of the "late cards look better" survivor effect.
    /// </summary>
    public (double Placement, int Games)? CardAtTurn(string cardId, int turn, int bracket)
    {
        foreach (var b in bracket == 100 ? [100] : new[] { bracket, 100 })
        {
            if (!CardsByBracket.TryGetValue(b, out var cards) || !cards.TryGetValue(cardId, out var card))
            {
                continue;
            }

            for (var spread = 0; spread <= 3; spread++)
            {
                var games = 0;
                var sum = 0.0;
                for (var t = turn - spread; t <= turn + spread; t++)
                {
                    if (card.Turns.TryGetValue(t, out var stats))
                    {
                        games += stats.Played;
                        sum += stats.Placement * stats.Played;
                    }
                }

                if (games >= MinCardGames)
                {
                    return (sum / games, games);
                }
            }
        }

        return null;
    }

    public IReadOnlyDictionary<string, MetaTrinket> Trinkets { get; }

    /// <summary>Trinket placement at the bracket, falling back to all ranks when the bracket sample is thin.</summary>
    public (double Placement, double PickRate, int DataPoints)? Trinket(string cardId, int bracket)
    {
        if (!Trinkets.TryGetValue(cardId, out var trinket) || trinket.DataPoints < 50)
        {
            return null;
        }

        return trinket.PlacementAtMmr.TryGetValue(bracket, out var at) && at.DataPoints >= 50
            ? (at.Placement, trinket.PickRate, at.DataPoints)
            : (trinket.AveragePlacement, trinket.PickRate, trinket.DataPoints);
    }

    public IReadOnlyDictionary<int, IReadOnlyDictionary<string, MetaHero>> HeroesByBracket { get; }

    public IReadOnlyList<(int Percentile, int Mmr)> Percentiles { get; }

    public IReadOnlyList<MetaComp> Comps { get; }

    public DateTime Updated { get; }

    /// <summary>Tightest MMR bracket (top X%) whose floor the rating reaches.</summary>
    public int BracketFor(int? rating)
    {
        if (rating is not > 0)
        {
            return 100;
        }

        var best = 100;
        foreach (var (percentile, mmr) in Percentiles)
        {
            if (rating >= mmr && percentile < best && HeroesByBracket.ContainsKey(percentile))
            {
                best = percentile;
            }
        }

        return best;
    }

    public MetaHero? Hero(string baseHeroId, int bracket)
    {
        if (HeroesByBracket.TryGetValue(bracket, out var heroes) && heroes.TryGetValue(baseHeroId, out var hero) && hero.DataPoints >= 100)
        {
            return hero;
        }

        return HeroesByBracket.TryGetValue(100, out var all) ? all.GetValueOrDefault(baseHeroId) : null;
    }

    public static async Task<BattlegroundsMeta?> LoadAsync(HttpClient http, CancellationToken cancellationToken = default)
    {
        try
        {
            var heroes = new Dictionary<int, IReadOnlyDictionary<string, MetaHero>>();
            var percentiles = new List<(int, int)>();
            DateTime updated = default;
            foreach (var bracket in Brackets)
            {
                var doc = await FetchAsync(http, $"api/bgs/hero-stats/mmr-{bracket}/last-patch/overview-from-hourly.gz.json", $"heroes-{bracket}.json", cancellationToken).ConfigureAwait(false);
                if (doc is null)
                {
                    continue;
                }

                using (doc)
                {
                    heroes[bracket] = ParseHeroes(doc.RootElement);
                    if (bracket == 100)
                    {
                        if (doc.RootElement.TryGetProperty("mmrPercentiles", out var pct) && pct.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var p in pct.EnumerateArray())
                            {
                                percentiles.Add((p.GetProperty("percentile").GetInt32(), p.GetProperty("mmr").GetInt32()));
                            }
                        }

                        if (doc.RootElement.TryGetProperty("lastUpdateDate", out var date) && date.TryGetDateTime(out var parsed))
                        {
                            updated = parsed;
                        }
                    }
                }
            }

            if (!heroes.ContainsKey(100))
            {
                return null;
            }

            var comps = new List<MetaComp>();
            using var strategies = await FetchAsync(http, "hearthstone/data/battlegrounds-strategies/bgs-comps-strategies.gz.json", "comp-strategies.json", cancellationToken).ConfigureAwait(false);
            using var compStats = await FetchAsync(http, "api/bgs/comp-stats/last-patch/overview-from-hourly.gz.json", "comp-stats.json", cancellationToken).ConfigureAwait(false);
            if (compStats is not null)
            {
                comps.AddRange(ParseComps(compStats.RootElement, strategies?.RootElement));
            }

            var trinkets = new Dictionary<string, MetaTrinket>(StringComparer.Ordinal);
            using var trinketStats = await FetchAsync(http, "api/bgs/trinket-stats/last-patch/overview-from-hourly.gz.json", "trinket-stats.json", cancellationToken).ConfigureAwait(false);
            if (trinketStats is not null && trinketStats.RootElement.TryGetProperty("trinketStats", out var list) && list.ValueKind == JsonValueKind.Array)
            {
                foreach (var t in list.EnumerateArray())
                {
                    var byMmr = new Dictionary<int, (double, int)>();
                    if (t.TryGetProperty("averagePlacementAtMmr", out var at) && at.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var m in at.EnumerateArray())
                        {
                            byMmr[m.GetProperty("mmr").GetInt32()] = (m.GetProperty("placement").GetDouble(), m.GetProperty("dataPoints").GetInt32());
                        }
                    }

                    var id = t.GetProperty("trinketCardId").GetString() ?? string.Empty;
                    trinkets[id] = new MetaTrinket(
                        id,
                        t.GetProperty("dataPoints").GetInt32(),
                        t.GetProperty("averagePlacement").GetDouble(),
                        t.TryGetProperty("pickRate", out var pr) ? pr.GetDouble() : 0,
                        byMmr);
                }
            }

            var cardStats = new Dictionary<int, IReadOnlyDictionary<string, MetaCard>>();
            foreach (var bracket in Brackets)
            {
                using var doc = await FetchAsync(http, $"api/bgs/card-stats/mmr-{bracket}/last-patch/overview-from-hourly.gz.json", $"cards-{bracket}.json", cancellationToken).ConfigureAwait(false);
                if (doc is not null)
                {
                    cardStats[bracket] = ParseCards(doc.RootElement);
                }
            }

            return new BattlegroundsMeta(heroes, percentiles, comps, trinkets, cardStats, updated);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            Diag.Error("Battlegrounds meta unreadable", ex);
            return null;
        }
    }

    private static Dictionary<string, MetaHero> ParseHeroes(JsonElement root)
    {
        var result = new Dictionary<string, MetaHero>(StringComparer.Ordinal);
        foreach (var hero in root.GetProperty("heroStats").EnumerateArray())
        {
            var impact = new Dictionary<int, double>();
            if (hero.TryGetProperty("tribeStats", out var tribes) && tribes.ValueKind == JsonValueKind.Array)
            {
                foreach (var tribe in tribes.EnumerateArray())
                {
                    impact[tribe.GetProperty("tribe").GetInt32()] = tribe.GetProperty("impactAveragePosition").GetDouble();
                }
            }

            var offered = hero.TryGetProperty("totalOffered", out var o) ? o.GetDouble() : 0;
            var picked = hero.TryGetProperty("totalPicked", out var p) ? p.GetDouble() : 0;
            var id = hero.GetProperty("heroCardId").GetString() ?? string.Empty;
            result[id] = new MetaHero(
                id,
                hero.GetProperty("dataPoints").GetInt32(),
                hero.GetProperty("averagePosition").GetDouble(),
                offered > 0 ? picked / offered : 0,
                impact);
        }

        return result;
    }

    private static IEnumerable<MetaComp> ParseComps(JsonElement stats, JsonElement? strategies)
    {
        var strategyById = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        if (strategies is { ValueKind: JsonValueKind.Array } list)
        {
            foreach (var s in list.EnumerateArray())
            {
                var id = s.GetProperty("compId").GetString()?.Trim();
                if (!string.IsNullOrEmpty(id))
                {
                    strategyById[id] = s;
                }
            }
        }

        foreach (var comp in stats.GetProperty("compStats").EnumerateArray())
        {
            var id = comp.GetProperty("archetype").GetString() ?? string.Empty;
            if (comp.GetProperty("dataPoints").GetInt32() < 500)
            {
                continue;
            }

            var atMmr = new Dictionary<int, double>();
            foreach (var m in comp.GetProperty("averagePlacementAtMmr").EnumerateArray())
            {
                if (m.GetProperty("dataPoints").GetInt32() >= 200)
                {
                    atMmr[m.GetProperty("mmr").GetInt32()] = m.GetProperty("placement").GetDouble();
                }
            }

            var heroPlacement = new Dictionary<string, (int, double)>(StringComparer.Ordinal);
            foreach (var h in comp.GetProperty("heroStats").EnumerateArray())
            {
                heroPlacement[h.GetProperty("heroCardId").GetString() ?? string.Empty] =
                    (h.GetProperty("dataPoints").GetInt32(), h.GetProperty("averagePlacement").GetDouble());
            }

            var cards = new List<MetaCompCard>();
            string name = Prettify(id), power = string.Empty, difficulty = string.Empty;
            string? when = null, tip = null;
            var tribes = new List<string>();
            if (strategyById.TryGetValue(id, out var strategy))
            {
                name = strategy.GetProperty("name").GetString() is { Length: > 0 } n ? n : name;
                power = strategy.TryGetProperty("powerLevel", out var pl) ? pl.GetString() ?? string.Empty : string.Empty;
                difficulty = strategy.TryGetProperty("difficulty", out var df) ? df.GetString() ?? string.Empty : string.Empty;
                foreach (var card in strategy.GetProperty("cards").EnumerateArray())
                {
                    cards.Add(new MetaCompCard(card.GetProperty("cardId").GetString() ?? string.Empty, card.GetProperty("status").GetString() ?? "ADDON"));
                }

                if (strategy.TryGetProperty("tips", out var tips) && tips.ValueKind == JsonValueKind.Array)
                {
                    var english = tips.EnumerateArray().FirstOrDefault(t => t.TryGetProperty("language", out var l) && l.GetString() == "enUS");
                    if (english.ValueKind == JsonValueKind.Object)
                    {
                        tip = english.TryGetProperty("tip", out var t) ? t.GetString() : null;
                        when = english.TryGetProperty("whenToCommit", out var w) ? w.GetString() : null;
                    }
                }

                if (strategy.TryGetProperty("forcedTribes", out var forced) && forced.ValueKind == JsonValueKind.Array)
                {
                    foreach (var f in forced.EnumerateArray())
                    {
                        if (f.ValueKind == JsonValueKind.Number && Cards.Races.ById.TryGetValue(f.GetInt32(), out var race))
                        {
                            tribes.Add(race);
                        }
                        else if (f.ValueKind == JsonValueKind.String && f.GetString() is { } raceName)
                        {
                            tribes.Add(raceName);
                        }
                    }
                }
            }

            var prefix = id.Split('_')[0];
            if (tribes.Count == 0 && PrefixTribes.TryGetValue(prefix, out var prefixTribe))
            {
                tribes.Add(prefixTribe);
            }

            yield return new MetaComp(
                id, name, power, difficulty, tribes, cards, when, tip,
                comp.GetProperty("averagePlacement").GetDouble(), atMmr, heroPlacement);
        }
    }

    private static Dictionary<string, MetaCard> ParseCards(JsonElement root)
    {
        var result = new Dictionary<string, MetaCard>(StringComparer.Ordinal);
        if (!root.TryGetProperty("cardStats", out var list) || list.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (var c in list.EnumerateArray())
        {
            if (c.GetProperty("cardId").GetString() is not { Length: > 0 } id
                || !c.TryGetProperty("averagePlacement", out var avg) || avg.ValueKind != JsonValueKind.Number)
            {
                continue;
            }

            var turns = new Dictionary<int, (int, double)>();
            if (c.TryGetProperty("turnStats", out var ts) && ts.ValueKind == JsonValueKind.Array)
            {
                foreach (var t in ts.EnumerateArray())
                {
                    if (t.TryGetProperty("turn", out var turn) && turn.ValueKind == JsonValueKind.Number
                        && t.TryGetProperty("totalPlayed", out var played) && played.ValueKind == JsonValueKind.Number
                        && t.TryGetProperty("averagePlacement", out var placement) && placement.ValueKind == JsonValueKind.Number)
                    {
                        turns[turn.GetInt32()] = (played.GetInt32(), placement.GetDouble());
                    }
                }
            }

            result[id] = new MetaCard(id, c.TryGetProperty("totalPlayed", out var total) ? total.GetInt32() : 0, avg.GetDouble(), turns);
        }

        return result;
    }

    private static string Prettify(string archetype) =>
        string.Join(' ', archetype.Split('_', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w == "apm" ? "APM" : char.ToUpperInvariant(w[0]) + w[1..]));

    private static async Task<JsonDocument?> FetchAsync(HttpClient http, string path, string cacheName, CancellationToken cancellationToken)
    {
        var dir = Directory.CreateDirectory(Path.Combine(AppPaths.Cache, "meta")).FullName;
        var file = Path.Combine(dir, cacheName);
        var info = new FileInfo(file);
        if (!info.Exists || DateTime.UtcNow - info.LastWriteTimeUtc > MaxAge)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, Root + path);
                using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
                    await File.WriteAllBytesAsync(file + ".tmp", bytes, cancellationToken).ConfigureAwait(false);
                    File.Move(file + ".tmp", file, overwrite: true);
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException)
            {
                Diag.Error($"Meta download failed ({path})", ex);
            }
        }

        if (!File.Exists(file))
        {
            return null;
        }

        await using var stream = File.OpenRead(file);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
