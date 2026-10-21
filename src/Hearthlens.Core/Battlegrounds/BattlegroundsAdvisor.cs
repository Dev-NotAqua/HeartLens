using Hearthlens.Core.Cards;
using Hearthlens.Core.Tracking;

namespace Hearthlens.Core.Battlegrounds;

/// <summary>
/// Turns community statistics plus the live lobby into recommendations: which hero to pick,
/// which composition to aim for, and which shop minion to buy next.
/// </summary>
public sealed class BattlegroundsAdvisor
{
    private static readonly Dictionary<string, int> RaceIds = Races.ById.ToDictionary(kv => kv.Value, kv => kv.Key);

    public BattlegroundsMeta? Meta { get; set; }

    /// <summary>Composition the player locked in from the UI; null lets the advisor choose.</summary>
    public string? PinnedCompId { get; set; }

    // The comp the advisor chose last time, so the suggestion does not flip on every small change.
    private string? _autoTargetId;
    private string? _autoTargetHero;
    private int _autoTargetTurn;

    public static string TierOf(double adjustedPosition) => adjustedPosition switch
    {
        < 3.75 => "S",
        < 4.05 => "A",
        < 4.35 => "B",
        < 4.65 => "C",
        _ => "D",
    };

    public BattlegroundsView Enrich(BattlegroundsView view, int? rating, CardDb cards)
    {
        var meta = Meta;
        if (meta is null)
        {
            return view;
        }

        var bracket = meta.BracketFor(rating);
        var lobbyKnown = view.Races.Count >= 4;
        var lobbyIds = view.Races.Select(r => RaceIds.GetValueOrDefault(r)).Where(id => id > 0).ToHashSet();
        var viable = meta.Comps.Where(c => !lobbyKnown || c.Tribes.All(view.Races.Contains)).ToList();

        var options = RankHeroes(view, cards, meta, bracket, lobbyKnown, lobbyIds, viable);

        IReadOnlyList<CompAdvice> comps = Array.Empty<CompAdvice>();
        IReadOnlyList<ShopPick> shop = Array.Empty<ShopPick>();
        IReadOnlyList<ShopPick> discover = Array.Empty<ShopPick>();
        IReadOnlyList<string> pairs = Array.Empty<string>();
        if (view.HeroCardId is { } hero)
        {
            var owned = OwnedCards(view, cards);
            if (hero != _autoTargetHero || view.Turn < _autoTargetTurn)
            {
                _autoTargetId = null;
            }

            _autoTargetHero = hero;
            _autoTargetTurn = view.Turn;
            comps = RankComps(view, viable, cards.BaseHeroId(hero), bracket, owned, cards, lobbyKnown);
            var target = comps.FirstOrDefault(c => c.IsTarget);
            shop = ScoreOffer(view, view.Shop.Select(m => new Offer(m.CardId, m.Name, m.TechLevel, "MINION")).ToList(),
                target, owned, cards, meta, bracket, dataWeight: 50);
            discover = ScoreOffer(view, view.DiscoverOptions.Select(d => new Offer(d.CardId, d.Name, d.TechLevel, d.CardType)).ToList(),
                target, owned, cards, meta, bracket, dataWeight: 80);
            pairs = owned.Where(kv => kv.Value == 2).Select(kv => cards.NameOf(kv.Key)).Where(n => n.Length > 0).ToList();
        }

        var percentile = bracket == 100 ? "all ranks" : $"top {bracket}%";
        return view with
        {
            HeroOptions = options,
            Comps = comps,
            ShopAdvice = shop,
            TrinketOptions = RankTrinkets(view.TrinketOptions, meta, bracket),
            DarkGiftAdvice = DarkGiftTip(view, shop),
            DiscoverAdvice = discover,
            FreezeAdvice = FreezeTip(view, shop),
            Pairs = pairs,
            MetaSource = $"{BattlegroundsMeta.Attribution} · {percentile} · {meta.Updated:dd MMM HH:mm}",
        };
    }

    private static List<BgHeroOption> RankHeroes(
        BattlegroundsView view, CardDb cards, BattlegroundsMeta meta, int bracket,
        bool lobbyKnown, HashSet<int> lobbyIds, List<MetaComp> viable)
    {
        var options = new List<(BgHeroOption Option, double Score)>();
        foreach (var option in view.HeroOptions)
        {
            var baseId = cards.BaseHeroId(option.CardId);
            HeroMeta? heroMeta = null;
            if (meta.Hero(baseId, bracket) is { } stats)
            {
                var adjusted = stats.AveragePosition + (lobbyKnown ? lobbyIds.Sum(t => stats.TribeImpact.GetValueOrDefault(t)) : 0);
                heroMeta = new HeroMeta(stats.AveragePosition, adjusted, stats.PickRate, stats.DataPoints, TierOf(adjusted));
            }

            var topComps = viable
                .Select(c => (c.Name, Placement: HeroCompPlacement(c, baseId, bracket)))
                .OrderBy(c => c.Placement)
                .Take(2)
                .Select(c => c.Name)
                .ToList();

            // Community data dominates; personal results nudge it once there is a sample.
            var score = heroMeta?.AdjustedPosition ?? 4.5;
            if (option.Stats is { Games: >= 5 } personal)
            {
                score = (score * 0.75) + (personal.AveragePlace * 0.25);
            }

            options.Add((option with { Meta = heroMeta, TopComps = topComps }, score));
        }

        var best = options.Where(o => o.Option.Meta is not null || o.Option.Stats is not null)
            .OrderBy(o => o.Score).Select(o => o.Option.CardId).FirstOrDefault();
        return options.Select(o => o.Option with { Recommended = o.Option.CardId == best }).ToList();
    }

    private static IReadOnlyList<BgTrinketOption> RankTrinkets(IReadOnlyList<BgTrinketOption> offer, BattlegroundsMeta meta, int bracket)
    {
        if (offer.Count == 0)
        {
            return offer;
        }

        var ranked = offer
            .Select(t => meta.Trinket(t.CardId, bracket) is { } stats
                ? t with { Meta = new TrinketMeta(stats.Placement, stats.PickRate, stats.DataPoints, TierOf(stats.Placement)) }
                : t)
            .ToList();
        var best = ranked.Where(t => t.Meta is not null).MinBy(t => t.Meta!.AveragePlacement)?.CardId;
        return ranked.Select(t => t with { Recommended = t.CardId == best }).ToList();
    }

    /// <summary>
    /// The Dark Gift button discovers a minion carrying a Dark Gift. It beats a filler buy, but a shop
    /// card that completes the plan (core card, triple) is still the better use of the gold.
    /// </summary>
    /// <summary>A key card is in the tavern but there is no gold left to buy it this turn.</summary>
    private static string FreezeTip(BattlegroundsView view, IReadOnlyList<ShopPick> shop)
    {
        const int MinionCost = 3;
        if (view.InCombat || view.Gold >= MinionCost || view.DiscoverOptions.Count > 0)
        {
            return string.Empty;
        }

        var keep = shop.Where(p => p.Score >= 60).Select(p => p.Name).Distinct().Take(2).ToList();
        return keep.Count == 0 ? string.Empty : $"Freeze: keep {string.Join(" and ", keep)} for next turn";
    }

    private static string DarkGiftTip(BattlegroundsView view, IReadOnlyList<ShopPick> shop)
    {
        if (!view.DarkGiftReady || view.InCombat)
        {
            return string.Empty;
        }

        var cost = view.DarkGiftCost > 0 ? view.DarkGiftCost : 3;
        if (view.Gold > 0 && view.Gold < cost)
        {
            return string.Empty;
        }

        var best = shop.FirstOrDefault(p => p.Best);
        return best is { Score: >= 60 }
            ? $"Dark Gift: buy {best.Name} first, then discover if gold is left"
            : $"Dark Gift: worth using now ({cost} gold) — beats the shop";
    }

    private static double HeroCompPlacement(MetaComp comp, string baseHeroId, int bracket)
    {
        var general = comp.PlacementAtMmr.TryGetValue(bracket, out var p) ? p : comp.AveragePlacement;
        if (comp.HeroPlacement.TryGetValue(baseHeroId, out var hero) && hero.DataPoints >= 40)
        {
            // Shrink small hero samples toward the comp's general placement.
            var weight = Math.Min(1.0, hero.DataPoints / 200.0);
            return (hero.Placement * weight) + (general * (1 - weight));
        }

        return general;
    }

    private IReadOnlyList<CompAdvice> RankComps(
        BattlegroundsView view, List<MetaComp> viable, string baseHero, int bracket, Dictionary<string, int> owned, CardDb cards, bool lobbyKnown)
    {
        var ranked = viable
            .Select(c =>
            {
                var ownedCore = c.Cards.Count(card => card.Status == "CORE" && owned.ContainsKey(card.CardId));
                var ownedAddon = c.Cards.Count(card => card.Status != "CORE" && owned.ContainsKey(card.CardId));
                var placement = HeroCompPlacement(c, baseHero, bracket);
                return (Comp: c, Placement: placement, OwnedCore: ownedCore, Weight: (ownedCore * 2) + ownedAddon,
                    Score: placement - (0.15 * ownedCore) - (0.05 * ownedAddon));
            })
            .OrderBy(x => x.Score)
            .ToList();

        if (ranked.Count == 0)
        {
            return Array.Empty<CompAdvice>();
        }

        var target = ranked.FirstOrDefault(x => x.Comp.Id == PinnedCompId);
        if (target.Comp is null)
        {
            target = ranked.Where(x => x.Weight >= 2 && x.Comp.Cards.Count > 0).OrderByDescending(x => x.Weight).ThenBy(x => x.Score).FirstOrDefault();
        }

        if (target.Comp is null)
        {
            target = ranked.FirstOrDefault(x => x.Comp.Cards.Count > 0);
        }

        // Stay on the previous suggestion unless the new one is ahead by at least one core card.
        var previous = ranked.FirstOrDefault(x => x.Comp.Id == _autoTargetId);
        if (PinnedCompId is null && previous.Comp is not null && target.Comp is not null && previous.Comp.Id != target.Comp.Id
            && target.Weight - previous.Weight < 2)
        {
            target = previous;
        }

        if (PinnedCompId is null)
        {
            _autoTargetId = target.Comp?.Id;
        }

        var shown = ranked.Take(5).ToList();
        if (target.Comp is not null && !shown.Any(x => x.Comp.Id == target.Comp.Id))
        {
            shown[^1] = target;
        }

        return shown.Select(x => new CompAdvice(
                x.Comp.Id,
                x.Comp.Name,
                x.Comp.PowerLevel,
                x.Comp.Difficulty,
                x.Comp.PlacementAtMmr.TryGetValue(bracket, out var p) ? p : x.Comp.AveragePlacement,
                x.Comp.HeroPlacement.TryGetValue(baseHero, out var h) && h.DataPoints >= 40 ? h.Placement : null,
                x.Comp.Tribes,
                x.Comp.Cards
                    .Select(card => new CompCard(card.CardId, cards.NameOf(card.CardId), card.Status, cards.Get(card.CardId)?.TechLevel ?? 0, owned.ContainsKey(card.CardId)))
                    .OrderBy(card => card.Status switch { "CORE" => 0, "ADDON" => 1, _ => 2 }).ThenBy(card => card.TechLevel)
                    .ToList(),
                x.Comp.WhenToCommit,
                x.Comp.Tip,
                x.OwnedCore,
                target.Comp is not null && x.Comp.Id == target.Comp.Id,
                x.Comp.Id == PinnedCompId)
            {
                Why = WhyComp(x.Comp, x.OwnedCore, x.Comp.Id == PinnedCompId, lobbyKnown, owned, baseHero),
                LookFor = x.Comp.Cards
                    .Where(card => card.Status == "CORE" && !owned.ContainsKey(card.CardId))
                    .Select(card => (Name: cards.NameOf(card.CardId), Tier: cards.Get(card.CardId)?.TechLevel ?? 0))
                    .Where(card => card.Name.Length > 0 && card.Tier > 0 && card.Tier <= Math.Max(view.TechLevel, 1) + 1)
                    .OrderBy(card => card.Tier)
                    .Take(3)
                    .Select(card => $"{card.Name} (T{card.Tier})")
                    .ToList(),
            })
            .ToList();
    }

    private static string WhyComp(MetaComp comp, int ownedCore, bool pinned, bool lobbyKnown, Dictionary<string, int> owned, string baseHero)
    {
        if (pinned)
        {
            return "Locked by you: the advisor will not switch away from it";
        }

        var core = comp.Cards.Count(card => card.Status == "CORE");
        var addons = comp.Cards.Count(card => card.Status != "CORE" && owned.ContainsKey(card.CardId));
        if (ownedCore > 0)
        {
            return $"You already have {ownedCore} of {core} core cards" + (addons > 0 ? $" and {addons} support card{(addons == 1 ? string.Empty : "s")}" : string.Empty);
        }

        if (addons > 0)
        {
            return $"You have {addons} of its support cards";
        }

        var hero = comp.HeroPlacement.TryGetValue(baseHero, out var h) && h.DataPoints >= 40 ? " with your hero" : string.Empty;
        return lobbyKnown
            ? $"Best average placement{hero} with this lobby's tribes"
            : $"Best average placement{hero} (lobby tribes not known yet)";
    }

    /// <summary>A card on offer: a tavern minion or one option of a Discover-style choice.</summary>
    private sealed record Offer(string CardId, string Name, int TechLevel, string CardType);

    /// <summary>
    /// Scores cards on offer (the tavern, or any Discover-style choice) against the target comp and the board,
    /// then shifts each score by community data: how players who played the card around this turn placed,
    /// compared with the other cards on offer. <paramref name="dataWeight"/> is the score per placement step.
    /// </summary>
    private static IReadOnlyList<ShopPick> ScoreOffer(
        BattlegroundsView view, IReadOnlyList<Offer> offer, CompAdvice? target, Dictionary<string, int> owned, CardDb cards,
        BattlegroundsMeta meta, int bracket, double dataWeight)
    {
        if (offer.Count == 0)
        {
            return Array.Empty<ShopPick>();
        }

        var dominantTribe = view.Board
            .SelectMany(m => cards.Get(m.CardId)?.Races ?? Array.Empty<string>())
            .Where(r => r != "ALL")
            .GroupBy(r => r).OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault();

        var picks = new List<ShopPick>();
        var slot = 0;
        foreach (var item in offer)
        {
            var id = NormalId(item.CardId, cards);
            var card = cards.Get(id);
            var tier = card?.TechLevel is > 0 and var t ? t : item.TechLevel;
            var (score, reasons) = item.CardType == "MINION"
                ? ScoreMinion(view, id, card, tier, target, owned, dominantTribe)
                : (0, new List<string>());
            var data = meta.CardAtTurn(id, Math.Max(1, view.Turn), bracket);
            picks.Add(new ShopPick(item.CardId, item.Name, tier, score, string.Join(" · ", reasons), false, slot++)
            {
                Placement = data?.Placement,
                PlacementGames = data?.Games ?? 0,
            });
        }

        // Community data only ranks cards against each other, so it needs at least two cards with data.
        var withData = picks.Where(p => p.Placement is not null).ToList();
        if (withData.Count >= 2)
        {
            var mean = withData.Average(p => p.Placement!.Value);
            var cap = dataWeight / 2;
            picks = picks
                .Select(p => p.Placement is { } placement
                    ? p with { Score = p.Score + (int)Math.Round(Math.Clamp((mean - placement) * dataWeight, -cap, cap)) }
                    : p)
                .ToList();
        }

        // Without data or any comp signal every option looks the same: show no pick rather than a guess.
        var decisive = withData.Count >= 2 || picks.Select(p => p.Score).Distinct().Count() > 1;
        var bestScore = picks.Max(p => p.Score);
        return picks
            .OrderByDescending(p => p.Score)
            .Select(p => p with { Best = decisive && p.Score == bestScore })
            .ToList();
    }

    private static (int Score, List<string> Reasons) ScoreMinion(
        BattlegroundsView view, string id, Card? card, int tier, CompAdvice? target, Dictionary<string, int> owned, string? dominantTribe)
    {
        var score = 0;
        var reasons = new List<string>();
        if (owned.GetValueOrDefault(id) >= 2)
        {
            score += 70;
            reasons.Add("Triple");
        }
        else if (owned.GetValueOrDefault(id) == 1)
        {
            score += 12;
            reasons.Add("Pair");
        }

        var compCard = target?.Cards.FirstOrDefault(c => c.CardId == id);
        if (compCard is not null)
        {
            var (points, label) = compCard.Status switch
            {
                "CORE" => (60, "Core"),
                "ADDON" => (30, "Add-on"),
                _ => (15, "Cycle"),
            };
            score += points;
            reasons.Add($"{label} · {target!.Name}");
        }

        var races = card?.Races ?? Array.Empty<string>();
        if (target is not null && target.Tribes.Count > 0 && races.Any(r => r == "ALL" || target.Tribes.Contains(r)))
        {
            score += 15;
            if (compCard is null) reasons.Add("Fits tribe");
        }
        else if (dominantTribe is not null && races.Any(r => r == "ALL" || r == dominantTribe))
        {
            score += 8;
            reasons.Add("Board synergy");
        }

        score += tier * 3;
        if (reasons.Count == 0)
        {
            reasons.Add(tier >= view.TechLevel ? "Best stats available" : "Filler");
        }

        return (score, reasons);
    }

    private static Dictionary<string, int> OwnedCards(BattlegroundsView view, CardDb cards)
    {
        var owned = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var id in view.Board.Select(m => m.CardId).Concat(view.Hand))
        {
            var normal = NormalId(id, cards);
            // A golden copy already consumed its triple; count it once for comp progress only.
            owned[normal] = owned.GetValueOrDefault(normal) + (normal == id ? 1 : 0);
            if (normal != id && owned[normal] == 0)
            {
                owned[normal] = 0;
            }
        }

        return owned;
    }

    private static string NormalId(string cardId, CardDb cards) =>
        cards.Get(cardId) is { BattlegroundsNormalDbfId: > 0 } golden && cards.Get(golden.BattlegroundsNormalDbfId) is { } normal
            ? normal.Id
            : cardId;
}
