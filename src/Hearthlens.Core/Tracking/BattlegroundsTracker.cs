using Hearthlens.Core.Battlegrounds;
using Hearthlens.Core.Cards;
using Hearthlens.Core.Power;

namespace Hearthlens.Core.Tracking;

/// <summary>Battlegrounds state: tavern tier, lobby, opponents' last seen boards, combats.</summary>
public sealed class BattlegroundsTracker : IPowerListener
{
    private const string PlaceholderHero = "TB_BaconShop_HERO_PH";

    private static readonly (string Tag, string Label)[] KeywordTags =
    [
        (GameTag.Taunt, "TAUNT"),
        (GameTag.DivineShield, "DIVINE_SHIELD"),
        (GameTag.Poisonous, "POISONOUS"),
        (GameTag.Venomous, "VENOMOUS"),
        (GameTag.Reborn, "REBORN"),
        (GameTag.Windfury, "WINDFURY"),
        (GameTag.Stealth, "STEALTH"),
    ];

    private readonly Dictionary<int, SeenBoard> _boards = new();
    private readonly List<CombatRecord> _combats = new();
    private readonly List<TierStep> _tiers = new();
    private readonly HashSet<string> _shopRaces = new(StringComparer.Ordinal);

    private bool _inCombat;
    private bool _boardCaptured;
    private int _combatOpponent;
    private int _localHealthAtCombat;
    private int _opponentHealthAtCombat;
    private int _pendingDamage = -1;
    private int _localAlive;
    private int _opponentAlive;
    private IReadOnlyList<BgMinion> _lastOwnBoard = Array.Empty<BgMinion>();
    private IReadOnlyList<Entity> _trinketOffer = Array.Empty<Entity>();
    private IReadOnlyList<Entity> _discoverOffer = Array.Empty<Entity>();
    private Entity? _discoverSource;
    private CombatOdds? _combatOdds;

    // Card effects the combat simulator does not model; boards with them get an "estimate" flag.
    private static readonly string[] UnsimulatedMechanics = ["DEATHRATTLE", "TRIGGER_VISUAL", "AURA", "AVENGE", "OVERKILL"];

    public CardDb Cards { get; set; } = CardDb.Empty;

    public Func<string, HeroStats?>? HeroStatsProvider { get; set; }

    /// <summary>Tribes read from client memory (authoritative when present).</summary>
    public IReadOnlyList<string>? MemoryRaces { get; set; }

    public bool LocalFinished { get; private set; }

    public void OnGameStarted(GameState game)
    {
        _boards.Clear();
        _combats.Clear();
        _tiers.Clear();
        _shopRaces.Clear();
        _inCombat = false;
        _boardCaptured = false;
        _lastOwnBoard = Array.Empty<BgMinion>();
        _trinketOffer = Array.Empty<Entity>();
        _discoverOffer = Array.Empty<Entity>();
        _discoverSource = null;
        _combatOdds = null;
        MemoryRaces = null;
        LocalFinished = false;
        _pendingDamage = -1;
    }

    public void OnChoices(GameState game, string choiceType, Entity? source, IReadOnlyList<Entity> options)
    {
        // Lesser/Greater Trinket offers arrive as a GENERAL choice of trinket cards.
        if (options.Count > 0 && options.All(IsTrinket))
        {
            _trinketOffer = options.ToArray();
            _discoverOffer = Array.Empty<Entity>();
            _discoverSource = null;
            return;
        }

        // Every other "pick one of these cards" (Discover from hero powers, spells, minions, trinkets,
        // the triple reward or the Dark Gift button) offers freshly created cards set aside.
        if (choiceType == "GENERAL" && options.Count > 1 && options.All(e => e.Zone == "SETASIDE" && e.CardId.Length > 0))
        {
            _discoverOffer = options.ToArray();
            _discoverSource = source;
        }
    }

    public void OnChoiceMade(GameState game)
    {
        _discoverOffer = Array.Empty<Entity>();
        _trinketOffer = Array.Empty<Entity>();
        _discoverSource = null;
    }

    public void OnEntityCreated(GameState game, Entity entity) => NoteShopMinion(game, entity);

    public void OnEntityRevealed(GameState game, Entity entity) => NoteShopMinion(game, entity);

    public void OnTagChanged(GameState game, Entity entity, string tag, string previous, string value)
    {
        if (!game.IsBattlegrounds)
        {
            return;
        }

        switch (tag)
        {
            case GameTag.BoardVisualState when entity.Id == game.GameEntityId:
                if (value == "2") BeginCombat(game);
                else if (_inCombat) EndCombat(game);
                break;

            case GameTag.PlayerTechLevel when IsLocal(game, entity):
                var tier = entity.GetInt(GameTag.PlayerTechLevel);
                if (tier > 0 && (_tiers.Count == 0 || _tiers[^1].Tier != tier))
                {
                    _tiers.Add(new TierStep(game.BattlegroundsTurn, tier));
                }

                break;

            case GameTag.Damage or GameTag.Armor or GameTag.Health when entity.IsHero && !_inCombat:
                ResolvePendingDamage(game);
                break;

            case GameTag.PlayState when entity.Id == game.LocalPlayer?.EntityId:
                if (value is "WON" or "LOST" or "TIED" or "CONCEDED")
                {
                    LocalFinished = true;
                }

                break;
        }
    }

    public void OnBlockStart(GameState game, string blockType, Entity? source)
    {
        if (_inCombat && !_boardCaptured && blockType == "ATTACK")
        {
            CaptureBoards(game, source);
        }
    }

    public void OnBlockEnd(GameState game)
    {
        if (_inCombat && _boardCaptured)
        {
            _localAlive = Minions(game, game.LocalPlayerId).Count();
            _opponentAlive = Minions(game, game.OpponentPlayerId).Count();
        }
    }

    public BattlegroundsView BuildView(GameState game)
    {
        // Combat as the player sees it: from the server resolving it until the client finishes playing it.
        var combat = _inCombat || game.CombatOnScreen;
        var localHero = game.LocalHero;
        var heroChosen = localHero is not null && localHero.CardId != PlaceholderHero && !localHero.InHand;
        var options = heroChosen
            ? new List<BgHeroOption>()
            : game.Controlled(game.LocalPlayerId, "HAND")
                .Where(e => e.IsHero && e.CardId.Length > 0 && e.CardId != PlaceholderHero)
                .OrderBy(e => e.ZonePosition)
                .Select(e => new BgHeroOption(
                    e.CardId,
                    Cards.NameOr(e.CardId, e.Name),
                    e.GetInt(GameTag.Armor) is > 0 and var a ? a : Cards.Get(e.CardId)?.Armor ?? 0,
                    HeroStatsProvider?.Invoke(Cards.BaseHeroId(e.CardId))))
                .ToList();

        var nextOpponent = localHero?.GetInt(GameTag.NextOpponentPlayerId) ?? 0;
        if (nextOpponent == 0)
        {
            nextOpponent = game.LocalPlayerEntity?.GetInt(GameTag.NextOpponentPlayerId) ?? 0;
        }

        var techLevel = game.LocalPlayerEntity?.GetInt(GameTag.PlayerTechLevel) ?? 0;
        if (techLevel == 0)
        {
            techLevel = localHero?.GetInt(GameTag.PlayerTechLevel) ?? 0;
        }

        var localBgId = localHero?.GetInt(GameTag.PlayerId) ?? 0;
        var opponents = LeaderboardHeroes(game)
            .Where(h => h.GetInt(GameTag.PlayerId) != localBgId)
            .Select(h =>
            {
                var playerId = h.GetInt(GameTag.PlayerId);
                _boards.TryGetValue(playerId, out var seen);
                var last = _combats.LastOrDefault(c => c.OpponentPlayerId == playerId);
                return new BgOpponent(
                    playerId,
                    h.CardId,
                    Cards.NameOr(h.CardId, h.Name),
                    h.Health,
                    h.GetInt(GameTag.Armor),
                    h.GetInt(GameTag.PlayerTechLevel),
                    h.GetInt(GameTag.PlayerLeaderboardPlace),
                    h.Health <= 0,
                    h.GetInt(GameTag.PlayerTriples),
                    seen?.Minions ?? Array.Empty<BgMinion>(),
                    seen?.Turn ?? 0,
                    last?.Result,
                    last is null ? 0 : last.Result == "Loss" ? last.DamageTaken : last.DamageDealt,
                    playerId == nextOpponent);
            })
            .OrderBy(o => o.Place == 0 ? 99 : o.Place)
            .ToList();

        var player = game.LocalPlayerEntity;
        var darkGift = game.Controlled(game.LocalPlayerId, "PLAY").FirstOrDefault(e => e.CardId.Contains("DarkGift", StringComparison.Ordinal));
        var anomalyDbf = game.GameEntity?.GetInt(GameTag.BaconAnomaly) ?? 0;
        var maxTier = game.LocalPlayerEntity?.GetInt(GameTag.BaconMaxTechLevel) ?? 0;

        return new BattlegroundsView(
            game.BattlegroundsTurn,
            Math.Max(1, techLevel),
            maxTier > 0 ? maxTier : 6,
            heroChosen ? localHero!.CardId : null,
            localHero?.Health ?? 0,
            localHero?.GetInt(GameTag.Armor) ?? 0,
            localHero?.GetInt(GameTag.PlayerLeaderboardPlace) ?? 0,
            game.IsDuos,
            combat,
            !heroChosen && options.Count > 0,
            options,
            MemoryRaces ?? _shopRaces.OrderBy(r => r, StringComparer.Ordinal).ToList(),
            MemoryRaces is not null,
            opponents,
            _combats.ToList(),
            _tiers.ToList(),
            _inCombat ? _lastOwnBoard : Minions(game, game.LocalPlayerId).Select(ToMinion).ToList(),
            anomalyDbf > 0 ? Cards.Get(anomalyDbf)?.Id : null,
            game.LocalPlayerEntity?.GetInt(GameTag.PlayerTriples) ?? 0,
            // Outside combat the opponent slot holds Bob's current offer.
            combat || !heroChosen ? Array.Empty<BgMinion>() : Minions(game, game.OpponentPlayerId).Select(ToMinion).ToList(),
            game.Controlled(game.LocalPlayerId, "HAND").Where(e => e.IsMinion && e.CardId.Length > 0).Select(e => e.CardId).ToList())
        {
            // A choice offered while the fight is still on screen waits until the player can see it.
            TrinketOptions = heroChosen && !game.CombatOnScreen ? PendingTrinkets() : Array.Empty<BgTrinketOption>(),
            DiscoverOptions = heroChosen && !game.CombatOnScreen ? PendingDiscover(game) : Array.Empty<BgDiscoverOption>(),
            DiscoverSource = _discoverOffer.Count == 0 ? null : DiscoverSourceLabel(_discoverSource),
            CombatOdds = combat ? _combatOdds : null,
            Trinkets = game.Controlled(game.LocalPlayerId, "PLAY").Where(IsTrinket).Select(e => e.CardId).Distinct().ToList(),
            DarkGiftReady = darkGift is not null && darkGift.GetInt("EXHAUSTED") == 0 && !combat,
            DarkGiftCost = darkGift?.GetInt(GameTag.Cost) ?? 0,
            Gold = player is null ? 0 : Math.Max(0, player.GetInt("RESOURCES") + player.GetInt("TEMP_RESOURCES") - player.GetInt("RESOURCES_USED")),
        };
    }

    private static bool IsTrinket(Entity e) =>
        e.CardType == "BATTLEGROUND_TRINKET" || e.CardId.Contains("_MagicItem_", StringComparison.Ordinal);

    private IReadOnlyList<BgTrinketOption> PendingTrinkets()
    {
        // The offer closes once one of the options is taken (moves to play) or combat starts.
        if (_trinketOffer.Count == 0 || _inCombat || _trinketOffer.Any(e => e.Zone is "PLAY" or "GRAVEYARD" or "REMOVEDFROMGAME"))
        {
            _trinketOffer = Array.Empty<Entity>();
            return Array.Empty<BgTrinketOption>();
        }

        return _trinketOffer
            .Select(e => new BgTrinketOption(e.CardId, Cards.NameOr(e.CardId, e.Name)))
            .ToList();
    }

    private IReadOnlyList<BgDiscoverOption> PendingDiscover(GameState game)
    {
        // The choice closes when the player picks (or the pick leaves set-aside) or combat starts.
        if (_discoverOffer.Count == 0 || _inCombat || _discoverOffer.Any(e => e.Zone != "SETASIDE"))
        {
            _discoverOffer = Array.Empty<Entity>();
            return Array.Empty<BgDiscoverOption>();
        }

        return _discoverOffer
            .Select(e =>
            {
                var gift = game.Entities.GetValueOrDefault(e.GetInt("DARK_GIFT_ENTITY"));
                var giftName = gift is null || gift.CardId.Length == 0 ? null : Cards.NameOr(gift.CardId, gift.Name);
                var card = Cards.Get(e.CardId);
                var tier = e.GetInt(GameTag.TechLevel) is > 0 and var t ? t : card?.TechLevel ?? 0;
                var type = card?.Type is { Length: > 0 } cardType ? cardType : e.CardType;
                return new BgDiscoverOption(e.CardId, Cards.NameOr(e.CardId, e.Name), tier, giftName, type);
            })
            .ToList();
    }

    /// <summary>"Kind · name" of what offers the open choice, e.g. "Hero power · Murloc King".</summary>
    private string? DiscoverSourceLabel(Entity? source)
    {
        if (source is null || source.CardId.Length == 0)
        {
            return null;
        }

        var card = Cards.Get(source.CardId);
        var name = card?.Name is { Length: > 0 } n ? n : source.Name;
        var kind = source.CardId switch
        {
            var id when id.StartsWith("TB_BaconShop_Triples", StringComparison.Ordinal) => "Triple reward",
            var id when id.StartsWith("BG36_MidGameEffect", StringComparison.Ordinal) => "Dark Gift",
            _ => (card?.Type ?? source.CardType) switch
            {
                "HERO_POWER" => "Hero power",
                "MINION" => "Minion",
                "BATTLEGROUND_TRINKET" => "Trinket",
                "SPELL" or "BATTLEGROUND_SPELL" => "Spell",
                _ => "Discover",
            },
        };
        return kind is "Triple reward" or "Dark Gift" ? kind : $"{kind} · {name}";
    }

    /// <summary>Final board for history, captured at the last combat.</summary>
    public IReadOnlyList<BgMinion> LastOwnBoard => _lastOwnBoard;

    public int FinalTechLevel => _tiers.Count > 0 ? _tiers[^1].Tier : 1;

    private void BeginCombat(GameState game)
    {
        _inCombat = true;
        _boardCaptured = false;
        _combatOdds = null;
        _pendingDamage = -1;
        _combatOpponent = game.OpponentPlayerEntity?.GetInt(GameTag.CurrentCombatPlayerId) ?? 0;
        _localHealthAtCombat = Durability(game.LocalHero);
        _opponentHealthAtCombat = Durability(LeaderboardHero(game, _combatOpponent));
    }

    /// <summary>
    /// Hero damage lands after the combat burst (armor first). Re-derive the last combat's damage
    /// from the health+armor snapshot taken when it started until the next combat begins.
    /// </summary>
    private void ResolvePendingDamage(GameState game)
    {
        if (_pendingDamage < 0 || _pendingDamage >= _combats.Count)
        {
            return;
        }

        var combat = _combats[_pendingDamage];
        var taken = Math.Max(0, _localHealthAtCombat - Durability(game.LocalHero));
        var dealt = Math.Max(0, _opponentHealthAtCombat - Durability(LeaderboardHero(game, combat.OpponentPlayerId)));
        var result = taken > 0 ? "Loss" : dealt > 0 ? "Win" : combat.Result;
        _combats[_pendingDamage] = combat with { DamageTaken = taken, DamageDealt = dealt, Result = result };
    }

    private static int Durability(Entity? hero) => hero is null ? 0 : hero.Health + hero.GetInt(GameTag.Armor);

    private void CaptureBoards(GameState game, Entity? firstAttacker)
    {
        _boardCaptured = true;
        // Start-of-combat effects have resolved by the first attack, so these boards are what actually fights.
        var ourSim = Minions(game, game.LocalPlayerId).Select(ToSim).ToList();
        var theirSim = Minions(game, game.OpponentPlayerId).Select(ToSim).ToList();
        bool? oursFirst = firstAttacker is { IsMinion: true } a ? a.Controller == game.LocalPlayerId : null;
        _combatOdds = CombatSimulator.Run(ourSim, theirSim, oursFirst);
        // The combat opponent id is tagged just after the board switches to combat.
        _combatOpponent = game.OpponentPlayerEntity?.GetInt(GameTag.CurrentCombatPlayerId) is > 0 and var id ? id : _combatOpponent;
        _opponentHealthAtCombat = Durability(LeaderboardHero(game, _combatOpponent));

        var theirs = Minions(game, game.OpponentPlayerId).Select(ToMinion).ToList();
        _lastOwnBoard = Minions(game, game.LocalPlayerId).Select(ToMinion).ToList();
        _localAlive = _lastOwnBoard.Count;
        _opponentAlive = theirs.Count;
        if (_combatOpponent > 0)
        {
            _boards[_combatOpponent] = new SeenBoard(game.BattlegroundsTurn, theirs);
        }
    }

    private void EndCombat(GameState game)
    {
        _inCombat = false;
        if (_combatOpponent == 0)
        {
            return;
        }

        var result = _localAlive == 0 && _opponentAlive > 0 ? "Loss"
            : _opponentAlive == 0 && _localAlive > 0 ? "Win"
            : "Tie";
        var hero = LeaderboardHero(game, _combatOpponent)?.CardId ?? string.Empty;
        _combats.Add(new CombatRecord(game.BattlegroundsTurn, _combatOpponent, hero, result, 0, 0, _combatOdds));
        _pendingDamage = _combats.Count - 1;
        _combatOpponent = 0;
        ResolvePendingDamage(game);
    }

    private void NoteShopMinion(GameState game, Entity entity)
    {
        if (!game.IsBattlegrounds || _inCombat || entity.Controller != game.OpponentPlayerId || !entity.IsMinion)
        {
            return;
        }

        if (Cards.Get(entity.CardId) is { IsBattlegroundsPoolMinion: true } card)
        {
            foreach (var race in card.Races)
            {
                if (race != "ALL")
                {
                    _shopRaces.Add(race);
                }
            }
        }
    }

    private static bool IsLocal(GameState game, Entity entity) =>
        entity.Id == game.LocalPlayer?.EntityId || entity.Id == game.LocalHero?.Id;

    private static IEnumerable<Entity> Minions(GameState game, int controller) =>
        game.Entities.Values
            .Where(e => e.Controller == controller && e.InPlay && e.IsMinion)
            .OrderBy(e => e.ZonePosition);

    /// <summary>Opponents' persistent leaderboard heroes (not combat copies or hero choices).</summary>
    private static IEnumerable<Entity> LeaderboardHeroes(GameState game)
    {
        var local = game.LocalHero;
        return game.Entities.Values
            .Where(e => e.IsHero && e.GetInt(GameTag.PlayerId) > 0 && e.GetInt(GameTag.PlayerLeaderboardPlace) > 0
                        && (e.Zone == "SETASIDE" || e == local) && e.CardId != PlaceholderHero)
            .GroupBy(e => e.GetInt(GameTag.PlayerId))
            // Combat copies share PLAYER_ID; the persistent leaderboard entity is the oldest one.
            .Select(g => g.MinBy(e => e.Id)!);
    }

    private static Entity? LeaderboardHero(GameState game, int playerId) =>
        LeaderboardHeroes(game).FirstOrDefault(h => h.GetInt(GameTag.PlayerId) == playerId);

    private SimMinion ToSim(Entity e)
    {
        var card = Cards.Get(e.CardId);
        var mechanics = card?.Mechanics ?? Array.Empty<string>();
        var unsimulated = mechanics.Any(m => UnsimulatedMechanics.Contains(m)) || UnsimulatedMechanics.Any(e.Has);
        var attacks = e.Has("MEGA_WINDFURY") ? 4 : e.Has(GameTag.Windfury) ? 2 : 1;
        return new SimMinion(
            e.Attack,
            e.Health,
            e.Has(GameTag.Taunt),
            e.Has(GameTag.DivineShield),
            e.Has(GameTag.Poisonous),
            e.Has(GameTag.Venomous),
            e.Has(GameTag.Reborn),
            attacks,
            e.Has(GameTag.Stealth),
            card is { Attack: > 0 } ? card.Attack : e.Attack,
            unsimulated);
    }

    private BgMinion ToMinion(Entity e)
    {
        var keywords = KeywordTags.Where(k => e.Has(k.Tag)).Select(k => k.Label).ToList();
        return new BgMinion(
            e.CardId,
            Cards.NameOr(e.CardId, e.Name),
            e.Attack,
            e.Health,
            e.GetInt(GameTag.Premium) > 0,
            e.GetInt(GameTag.TechLevel),
            keywords);
    }

    private sealed record SeenBoard(int Turn, IReadOnlyList<BgMinion> Minions);
}
