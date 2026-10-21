using System.Globalization;
using Hearthlens.UI.Infrastructure;
using Hearthlens.Core;
using Hearthlens.Core.Cards;
using Hearthlens.Core.Data;
using Hearthlens.Core.Tracking;

namespace Hearthlens.UI.Overlay;

/// <summary>
/// Flattens live snapshots into display rows. Lists are only replaced when their content
/// changes, so item containers (and their images) survive the ~10 Hz snapshot stream.
/// </summary>
public sealed class OverlayViewModel : Observable
{
    private readonly Dictionary<string, string> _fingerprints = new();
    private BattlegroundsView? _bg;
    private CardDb _cards = CardDb.Empty;
    private string _locale = "enUS";

    // ---- visibility -----------------------------------------------------------------------
    private bool _showConstructed;
    private bool _showOpponent;
    private bool _showBattlegrounds;
    private bool _showHeroPick;
    private bool _showComp;
    private bool _showTrinkets;
    private bool _showShop;
    private bool _showDiscover;
    private bool _showOdds;
    private bool _compCollapsed = true;

    public bool ShowConstructed { get => _showConstructed; private set => Set(ref _showConstructed, value); }
    public bool ShowOpponent { get => _showOpponent; private set => Set(ref _showOpponent, value); }
    public bool ShowBattlegrounds { get => _showBattlegrounds; private set => Set(ref _showBattlegrounds, value); }
    public bool ShowHeroPick { get => _showHeroPick; private set => Set(ref _showHeroPick, value); }
    public bool ShowComp { get => _showComp; private set => Set(ref _showComp, value); }
    public bool ShowTrinkets { get => _showTrinkets; private set => Set(ref _showTrinkets, value); }
    public bool ShowShop { get => _showShop; private set => Set(ref _showShop, value); }
    public bool ShowDiscover { get => _showDiscover; private set => Set(ref _showDiscover, value); }
    public bool ShowOdds { get => _showOdds; private set => Set(ref _showOdds, value); }

    /// <summary>True hides the plan's secondary details (commit timing, tips, alternatives).</summary>
    public bool CompCollapsed { get => _compCollapsed; set => Set(ref _compCollapsed, value); }

    // ---- constructed ----------------------------------------------------------------------
    private string _deckName = string.Empty;
    private string _deckRecord = string.Empty;
    private string _deckCounts = string.Empty;
    private string _nextDraw = string.Empty;
    private IReadOnlyList<DeckTile> _deck = Array.Empty<DeckTile>();
    private bool _compactDeck;
    private bool _showChance = true;

    public string DeckName { get => _deckName; private set => Set(ref _deckName, value); }
    public string DeckRecord { get => _deckRecord; private set => Set(ref _deckRecord, value); }
    public string DeckCounts { get => _deckCounts; private set => Set(ref _deckCounts, value); }
    public string NextDraw { get => _nextDraw; private set => Set(ref _nextDraw, value); }
    public IReadOnlyList<DeckTile> Deck { get => _deck; private set => Set(ref _deck, value); }
    public bool CompactDeck { get => _compactDeck; private set => Set(ref _compactDeck, value); }
    public bool ShowChance { get => _showChance; private set => Set(ref _showChance, value); }

    private string _opponentTitle = string.Empty;
    private string _opponentSubtitle = string.Empty;
    private string _opponentHeroId = string.Empty;
    private int _opponentHand;
    private int _opponentDeck;
    private int _opponentSecrets;
    private string _opponentMatchup = string.Empty;
    private IReadOnlyList<PlayedTile> _played = Array.Empty<PlayedTile>();
    private IReadOnlyList<HandTile> _hand = Array.Empty<HandTile>();

    public string OpponentTitle { get => _opponentTitle; private set => Set(ref _opponentTitle, value); }
    public string OpponentSubtitle { get => _opponentSubtitle; private set => Set(ref _opponentSubtitle, value); }
    public string OpponentHeroId { get => _opponentHeroId; private set => Set(ref _opponentHeroId, value); }
    public int OpponentHand { get => _opponentHand; private set => Set(ref _opponentHand, value); }
    public int OpponentDeck { get => _opponentDeck; private set => Set(ref _opponentDeck, value); }
    public int OpponentSecrets { get => _opponentSecrets; private set => Set(ref _opponentSecrets, value); }
    public string OpponentMatchup { get => _opponentMatchup; private set => Set(ref _opponentMatchup, value); }
    public IReadOnlyList<PlayedTile> Played { get => _played; private set => Set(ref _played, value); }
    public IReadOnlyList<HandTile> Hand { get => _hand; private set => Set(ref _hand, value); }

    // ---- battlegrounds --------------------------------------------------------------------
    private int _turn;
    private string _phase = string.Empty;
    private IReadOnlyList<TierButton> _tiers = Array.Empty<TierButton>();
    private IReadOnlyList<string> _tribes = Array.Empty<string>();
    private string _rating = string.Empty;
    private string _ratingDelta = string.Empty;
    private int _ratingDeltaValue;
    private IReadOnlyList<CombatDot> _combats = Array.Empty<CombatDot>();
    private IReadOnlyList<HeroPickCard> _heroPicks = Array.Empty<HeroPickCard>();
    private CompRow? _target;
    private IReadOnlyList<CompCardTile> _targetCards = Array.Empty<CompCardTile>();
    private string _commitHint = string.Empty;
    private string _compTip = string.Empty;
    private IReadOnlyList<CompRow> _alternatives = Array.Empty<CompRow>();
    private IReadOnlyList<ShopBadge> _shopBadges = Array.Empty<ShopBadge>();
    private IReadOnlyList<TrinketBadge> _trinketPicks = Array.Empty<TrinketBadge>();
    private IReadOnlyList<string> _ownedTrinkets = Array.Empty<string>();
    private string _darkGift = string.Empty;
    private string _freeze = string.Empty;
    private IReadOnlyList<string> _pairs = Array.Empty<string>();
    private IReadOnlyList<DiscoverBadge> _discoverBadges = Array.Empty<DiscoverBadge>();
    private string _oddsWin = string.Empty;
    private string _oddsTie = string.Empty;
    private string _oddsLoss = string.Empty;
    private string _oddsNote = string.Empty;
    private double _winShare;
    private double _tieShare;
    private double _lossShare;
    private string _metaSource = string.Empty;
    private int _selectedTier;
    private IReadOnlyList<BrowserGroup> _browser = Array.Empty<BrowserGroup>();
    private string _anomaly = string.Empty;
    private LobbyPopup? _lobby;
    private double _lobbyTop;
    private double _lobbyLeft;
    private string _bracket = string.Empty;

    public int Turn { get => _turn; private set => Set(ref _turn, value); }
    public string Phase { get => _phase; private set => Set(ref _phase, value); }
    public IReadOnlyList<TierButton> Tiers { get => _tiers; private set => Set(ref _tiers, value); }
    public IReadOnlyList<string> Tribes { get => _tribes; private set => Set(ref _tribes, value); }
    public string Rating { get => _rating; private set => Set(ref _rating, value); }
    public string RatingDelta { get => _ratingDelta; private set => Set(ref _ratingDelta, value); }
    public int RatingDeltaValue { get => _ratingDeltaValue; private set => Set(ref _ratingDeltaValue, value); }
    public IReadOnlyList<CombatDot> Combats { get => _combats; private set => Set(ref _combats, value); }
    public IReadOnlyList<HeroPickCard> HeroPicks { get => _heroPicks; private set => Set(ref _heroPicks, value); }
    public CompRow? Target { get => _target; private set => Set(ref _target, value); }
    public IReadOnlyList<CompCardTile> TargetCards { get => _targetCards; private set => Set(ref _targetCards, value); }
    public string CommitHint { get => _commitHint; private set => Set(ref _commitHint, value); }
    public string CompTip { get => _compTip; private set => Set(ref _compTip, value); }
    public IReadOnlyList<CompRow> Alternatives { get => _alternatives; private set => Set(ref _alternatives, value); }
    public IReadOnlyList<ShopBadge> ShopBadges { get => _shopBadges; private set => Set(ref _shopBadges, value); }
    public IReadOnlyList<TrinketBadge> TrinketPicks { get => _trinketPicks; private set => Set(ref _trinketPicks, value); }
    public IReadOnlyList<string> OwnedTrinkets { get => _ownedTrinkets; private set => Set(ref _ownedTrinkets, value); }
    public string DarkGift { get => _darkGift; private set => Set(ref _darkGift, value); }
    public string Freeze { get => _freeze; private set => Set(ref _freeze, value); }
    public IReadOnlyList<string> Pairs { get => _pairs; private set => Set(ref _pairs, value); }
    public IReadOnlyList<DiscoverBadge> DiscoverBadges { get => _discoverBadges; private set => Set(ref _discoverBadges, value); }
    public string OddsWin { get => _oddsWin; private set => Set(ref _oddsWin, value); }
    public string OddsTie { get => _oddsTie; private set => Set(ref _oddsTie, value); }
    public string OddsLoss { get => _oddsLoss; private set => Set(ref _oddsLoss, value); }
    public string OddsNote { get => _oddsNote; private set => Set(ref _oddsNote, value); }
    /// <summary>Win / tie / loss shares (0..1) for the odds bar.</summary>
    public double WinShare { get => _winShare; private set => Set(ref _winShare, value); }
    public double TieShare { get => _tieShare; private set => Set(ref _tieShare, value); }
    public double LossShare { get => _lossShare; private set => Set(ref _lossShare, value); }
    public string MetaSource { get => _metaSource; private set => Set(ref _metaSource, value); }

    private string _discoverTitle = string.Empty;

    /// <summary>What the open choice comes from ("Hero power · …") and whether there is a clear best pick.</summary>
    public string DiscoverTitle { get => _discoverTitle; private set => Set(ref _discoverTitle, value); }
    public int SelectedTier { get => _selectedTier; private set => Set(ref _selectedTier, value); }
    public IReadOnlyList<BrowserGroup> Browser { get => _browser; private set => Set(ref _browser, value); }
    public string Anomaly { get => _anomaly; private set => Set(ref _anomaly, value); }
    public LobbyPopup? Lobby { get => _lobby; private set => Set(ref _lobby, value); }
    public double LobbyTop { get => _lobbyTop; private set => Set(ref _lobbyTop, value); }
    public double LobbyLeft { get => _lobbyLeft; private set => Set(ref _lobbyLeft, value); }
    public string Bracket { get => _bracket; private set => Set(ref _bracket, value); }

    /// <summary>Current Battlegrounds game is Duos (the leaderboard is laid out in teams).</summary>
    public bool IsDuos { get; private set; }

    // ---- lobby list, session, board -------------------------------------------------------
    private bool _showLobby;
    private bool _showSession;
    private IReadOnlyList<LobbyRow> _lobbyRows = Array.Empty<LobbyRow>();
    private string _sessionRating = string.Empty;
    private IReadOnlyList<PlaceChip> _sessionPlaces = Array.Empty<PlaceChip>();
    private IReadOnlyList<string> _boardTribes = Array.Empty<string>();
    private bool _layoutEditing;

    public bool ShowLobby { get => _showLobby; private set => Set(ref _showLobby, value); }
    public bool ShowSession { get => _showSession; private set => Set(ref _showSession, value); }
    public IReadOnlyList<LobbyRow> LobbyRows { get => _lobbyRows; private set => Set(ref _lobbyRows, value); }
    public string SessionRating { get => _sessionRating; private set => Set(ref _sessionRating, value); }
    public IReadOnlyList<PlaceChip> SessionPlaces { get => _sessionPlaces; private set => Set(ref _sessionPlaces, value); }
    public IReadOnlyList<string> BoardTribes { get => _boardTribes; private set => Set(ref _boardTribes, value); }

    /// <summary>Layout mode: every panel is shown with a frame and can be dragged, resized and reset.</summary>
    public bool LayoutEditing { get => _layoutEditing; set => Set(ref _layoutEditing, value); }

    public event Action? ResetLayoutRequested;

    // ---- toast ----------------------------------------------------------------------------
    private string _toastTitle = string.Empty;
    private string _toastDetail = string.Empty;
    private string _toastResult = string.Empty;
    private bool _showToast;

    public string ToastTitle { get => _toastTitle; private set => Set(ref _toastTitle, value); }
    public string ToastDetail { get => _toastDetail; private set => Set(ref _toastDetail, value); }
    public string ToastResult { get => _toastResult; private set => Set(ref _toastResult, value); }
    public bool ShowToast { get => _showToast; set => Set(ref _showToast, value); }

    /// <summary>Selected composition (pin) requested from the overlay.</summary>
    public event Action<string?>? PinRequested;

    public OverlayViewModel()
    {
        SelectTier = new RelayCommand(p =>
        {
            var tier = Convert.ToInt32(p, CultureInfo.InvariantCulture);
            SelectedTier = SelectedTier == tier ? 0 : tier;
            RebuildBrowser();
            RebuildTiers();
        });
        Pin = new RelayCommand(p =>
        {
            var id = p as string;
            // Clicking the pinned comp again releases it back to automatic choice.
            var pinned = Target is { IsPinned: true } t && t.Id == id;
            PinRequested?.Invoke(pinned ? null : id);
        });
        ToggleComp = new RelayCommand(() => CompCollapsed = !CompCollapsed);
        FinishLayout = new RelayCommand(() => LayoutEditing = false);
        ResetLayout = new RelayCommand(() => ResetLayoutRequested?.Invoke());
        CloseBrowser = new RelayCommand(() =>
        {
            SelectedTier = 0;
            RebuildBrowser();
            RebuildTiers();
        });
    }

    public RelayCommand SelectTier { get; }

    public RelayCommand Pin { get; }

    public RelayCommand ToggleComp { get; }

    public RelayCommand CloseBrowser { get; }

    public RelayCommand FinishLayout { get; }

    public RelayCommand ResetLayout { get; }

    public void Update(LiveSnapshot snapshot, CardDb cards, AppSettings settings, DeckAggregate? deck, MatchupAggregate? matchup)
    {
        _cards = cards;
        _locale = cards.Locale;
        var enabled = settings.OverlayEnabled && snapshot.InGame;

        var constructed = enabled ? snapshot.Constructed : null;
        ShowConstructed = constructed is not null && settings.ShowDeckTracker && constructed.DeckKnown;
        ShowOpponent = constructed is not null && settings.ShowOpponentTracker;
        ShowChance = settings.ShowDrawChance;
        if (constructed is not null)
        {
            UpdateConstructed(constructed, deck, matchup);
        }

        var bg = enabled ? snapshot.Battlegrounds : null;
        _bg = bg;
        ShowBattlegrounds = bg is not null && settings.ShowBattlegroundsBar;
        ShowHeroPick = bg is { IsHeroSelection: true } && settings.ShowHeroPick && bg.HeroOptions.Count > 0;
        var playing = bg is { IsHeroSelection: false, HeroCardId: not null } && settings.ShowHeroPick;
        ShowComp = playing && (bg!.Comps.Count > 0 || bg.Trinkets.Count > 0 || bg.DarkGiftAdvice.Length > 0 || bg.Pairs.Count > 0);
        ShowTrinkets = playing && bg!.TrinketOptions.Count > 0;
        ShowDiscover = playing && bg!.DiscoverAdvice.Count > 0;
        ShowOdds = settings.ShowCombatOdds && bg is { InCombat: true, CombatOdds: not null };
        // Buy hints sit on the tavern row, so only while recruiting and while no choice covers the shop.
        ShowShop = playing && !bg!.InCombat && bg.TrinketOptions.Count == 0 && bg.DiscoverOptions.Count == 0 && bg.ShopAdvice.Count > 0;
        ShowLobby = bg is { IsHeroSelection: false } && settings.ShowLobbyPanel && bg.Opponents.Count > 0;
        ShowSession = bg is not null && settings.ShowSessionPanel;
        if (bg is not null)
        {
            UpdateBattlegrounds(bg, snapshot);
            UpdateLobbyAndSession(bg, snapshot);
        }
        else if (SelectedTier != 0)
        {
            SelectedTier = 0;
            Browser = Array.Empty<BrowserGroup>();
        }

        if (bg is null)
        {
            Lobby = null;
        }
    }

    private void UpdateConstructed(ConstructedView view, DeckAggregate? deck, MatchupAggregate? matchup)
    {
        DeckName = view.DeckName ?? "Deck";
        DeckRecord = deck is { Games: > 0 } d ? $"{d.Wins}–{d.Losses} · {d.WinRate:P0}" : "New deck";
        DeckCounts = $"{view.DeckCount} in deck · {view.HandCount} in hand";
        var best = view.Deck.Where(r => r.Remaining > 0).MaxBy(r => r.DrawChance);
        NextDraw = best is null || view.DeckCount == 0 ? string.Empty : $"Next draw: {best.Name} {best.DrawChance:P0}";
        CompactDeck = view.Deck.Count > 24;
        Replace(nameof(Deck), view.Deck, r => $"{r.CardId}{r.Remaining}{r.Total}{r.DrawChance:0.00}", () => Deck = view.Deck
            .Select(r => new DeckTile(
                r.CardId,
                r.Name,
                r.Cost,
                r.Rarity == "LEGENDARY" && r.Total == 1 ? "★" : r.Remaining.ToString(CultureInfo.InvariantCulture),
                r.Total > 1 || r.Rarity == "LEGENDARY" || r.IsAdded,
                r.Remaining == 0,
                r.Rarity,
                r.Remaining > 0 && view.DeckCount > 0 ? $"{r.DrawChance * 100:0}%" : string.Empty,
                r.IsAdded))
            .ToList());

        OpponentHeroId = view.OpponentHeroCardId;
        OpponentTitle = string.IsNullOrEmpty(view.OpponentName) ? ClassName(view.OpponentClass) : view.OpponentName!;
        OpponentSubtitle = string.Join(" · ", new[] { ClassName(view.OpponentClass), view.OpponentRank?.Display }.Where(s => !string.IsNullOrEmpty(s)));
        OpponentHand = view.OpponentHandCount;
        OpponentDeck = view.OpponentDeckCount;
        OpponentSecrets = view.OpponentSecrets;
        OpponentMatchup = matchup is { Games: > 0 } m ? $"You vs {ClassName(view.OpponentClass)}: {m.Wins}–{m.Losses} ({m.WinRate:P0})" : string.Empty;
        Replace(nameof(Played), view.OpponentPlayed, p => $"{p.CardId}{p.Count}", () => Played = view.OpponentPlayed
            .Select(p => new PlayedTile(p.CardId, p.Name, p.Cost, p.Count > 1 ? $"×{p.Count}" : string.Empty, $"T{p.FirstTurn}", p.IsCreated))
            .ToList());
        Replace(nameof(Hand), view.OpponentHand, h => $"{h.Position}{h.Turn}{h.CardId}{h.CreatedBy}{h.FromMulligan}", () => Hand = view.OpponentHand
            .Select(h => new HandTile(
                h.Position.ToString(CultureInfo.InvariantCulture),
                h.Turn > 0 ? $"T{h.Turn}" : "M",
                h.IsCoin ? "The Coin" : h.CardName ?? (h.CreatedBy is not null ? $"Created by {h.CreatedBy}" : h.FromMulligan ? "Kept in mulligan" : "Unknown"),
                h.CardName is not null || h.IsCoin))
            .ToList());
    }

    private void UpdateBattlegrounds(BattlegroundsView view, LiveSnapshot snapshot)
    {
        Turn = view.Turn;
        IsDuos = view.IsDuos;
        Phase = view.IsHeroSelection ? "Hero selection" : view.InCombat ? "Combat" : "Recruit";
        var anomaly = view.AnomalyCardId is { } a ? _cards.NameOf(a) : string.Empty;
        Anomaly = anomaly;

        var rating = snapshot.Rating is null ? 0 : view.IsDuos ? snapshot.Rating.Duos : snapshot.Rating.Solo;
        Rating = rating > 0 ? rating.ToString("N0", CultureInfo.CurrentCulture) : "—";
        var delta = view.IsDuos ? null : snapshot.Session.Delta;
        RatingDeltaValue = delta ?? 0;
        RatingDelta = delta is { } dv && dv != 0 ? (dv > 0 ? $"+{dv}" : $"−{-dv}") : string.Empty;

        Replace(nameof(Tribes), view.Races, r => r, () => Tribes = view.Races.Select(r => Races.Display(r, _locale)).ToList());
        Combats = view.Combats.TakeLast(8)
            .Select(c => new CombatDot(c.Result, $"T{c.Turn} vs {_cards.NameOf(c.OpponentHeroCardId)} — {c.Result}{(c.DamageTaken > 0 ? $", took {c.DamageTaken}" : c.DamageDealt > 0 ? $", dealt {c.DamageDealt}" : string.Empty)}"
                + (c.Odds is { } o ? $" · predicted {o.Win:P0} win, {o.Loss:P0} loss" : string.Empty)))
            .ToList();

        if (view.CombatOdds is { } odds)
        {
            OddsWin = odds.Win.ToString("P0", CultureInfo.CurrentCulture);
            OddsTie = odds.Tie.ToString("P0", CultureInfo.CurrentCulture);
            OddsLoss = odds.Loss.ToString("P0", CultureInfo.CurrentCulture);
            WinShare = odds.Win;
            TieShare = odds.Tie;
            LossShare = odds.Loss;
            OddsNote = odds.Approximate
                ? $"estimate · {odds.Unsimulated} minion effect{(odds.Unsimulated == 1 ? string.Empty : "s")} not simulated"
                : "simulated from both boards";
        }
        RebuildTiers();

        // Lobby-adjusted numbers only mean something once the tribes are known.
        var lobbyKnown = view.Races.Count >= 4;
        Replace(nameof(HeroPicks), view.HeroOptions, h => $"{lobbyKnown}{h.CardId}{h.Meta?.AdjustedPosition:0.000}{h.Recommended}{string.Join(',', h.TopComps ?? [])}", () => HeroPicks = view.HeroOptions
            .Select(h =>
            {
                var adjusted = h.Meta is not null && lobbyKnown;
                var delta = adjusted ? h.Meta!.AdjustedPosition - h.Meta.AveragePosition : 0;
                return new HeroPickCard(
                    h.CardId,
                    h.Name,
                    h.Meta?.Tier ?? "–",
                    h.Meta is null ? "—" : (adjusted ? h.Meta.AdjustedPosition : h.Meta.AveragePosition).ToString("0.00", CultureInfo.CurrentCulture),
                    h.Meta is null ? "no data" : adjusted ? "avg place, this lobby" : "avg place",
                    Math.Abs(delta) < 0.005 ? string.Empty : delta < 0 ? $"▲{-delta:0.00}" : $"▼{delta:0.00}",
                    -delta,
                    h.Meta is null ? string.Empty : $"{h.Meta.PickRate.ToString("P0", CultureInfo.CurrentCulture)} pick",
                    h.Stats is { Games: > 0 } s ? $"You: {s.AveragePlace:0.0} avg in {s.Games}" : string.Empty,
                    string.Join(" · ", (h.TopComps ?? Array.Empty<string>()).Take(2)),
                    h.Recommended,
                    h.Meta is not null);
            })
            .ToList());

        Replace(nameof(TrinketPicks), view.TrinketOptions, t => $"{t.CardId}{t.Meta?.AveragePlacement:0.000}{t.Recommended}", () => TrinketPicks = view.TrinketOptions
            .Select(t => new TrinketBadge(
                t.CardId,
                t.Name,
                t.Meta?.Tier ?? "–",
                t.Meta is null ? "—" : t.Meta.AveragePlacement.ToString("0.00", CultureInfo.CurrentCulture),
                t.Meta is null ? "no data" : $"{t.Meta.PickRate.ToString("P0", CultureInfo.CurrentCulture)} pick",
                t.Recommended,
                t.Meta is not null))
            .ToList());

        Replace(nameof(OwnedTrinkets), view.Trinkets, t => t, () => OwnedTrinkets = view.Trinkets.Select(_cards.NameOf).ToList());
        DarkGift = view.DarkGiftAdvice;
        Freeze = view.FreezeAdvice;
        Replace(nameof(Pairs), view.Pairs, p => p, () => Pairs = view.Pairs.ToList());

        // One slot per option in screen order, so each badge lands above its own card.
        Replace(nameof(DiscoverBadges), view.DiscoverAdvice, d => $"{d.Slot}{d.Score}{d.Best}{d.Reason}{d.Placement:0.000}", () =>
        {
            var gifts = view.DiscoverOptions;
            var ranked = view.DiscoverAdvice.OrderByDescending(d => d.Score).Select(d => d.Slot).ToList();
            DiscoverBadges = view.DiscoverAdvice
                .OrderBy(d => d.Slot)
                .Select(d => new DiscoverBadge(
                    ranked.IndexOf(d.Slot) + 1,
                    ShortReason(d.Reason),
                    d.Slot < gifts.Count ? gifts[d.Slot].DarkGift ?? string.Empty : string.Empty,
                    d.Placement is { } p ? p.ToString("0.00", CultureInfo.CurrentCulture) : "—",
                    d.Placement is null ? "no data yet" : "avg place",
                    d.Best,
                    d.Placement is not null))
                .ToList();
        });
        var source = view.DiscoverSource ?? "Discover";
        DiscoverTitle = view.DiscoverAdvice.Count == 0
            ? string.Empty
            : view.DiscoverAdvice.Any(d => d.Best)
                ? $"{source}  ·  best pick marked"
                : $"{source}  ·  close call, no clear best";

        MetaSource = view.MetaSource ?? string.Empty;
        Bracket = view.MetaSource?.Split(" · ").ElementAtOrDefault(1) ?? string.Empty;

        var target = view.Comps.FirstOrDefault(c => c.IsTarget);
        Replace(nameof(Target), view.Comps, c => $"{c.Id}{c.IsTarget}{c.IsPinned}{c.OwnedCore}{c.Why}{string.Join(',', c.LookFor)}{string.Concat(c.Cards.Select(x => x.Owned ? '1' : '0'))}", () =>
        {
            Target = target is null ? null : Row(target);
            TargetCards = target?.Cards
                .Select(c => new CompCardTile(c.CardId, c.Name, c.Status, c.Status switch { "CORE" => "Core", "ADDON" => "Add-on", _ => "Cycle" }, c.TechLevel, c.Owned))
                .ToList() ?? (IReadOnlyList<CompCardTile>)Array.Empty<CompCardTile>();
            CommitHint = target?.WhenToCommit ?? string.Empty;
            CompTip = target?.Tip ?? string.Empty;
            Alternatives = view.Comps.Where(c => !c.IsTarget).Select(Row).ToList();
            if (SelectedTier > 0)
            {
                RebuildBrowser();
            }
        });

        var shopSize = view.Shop.Count;
        Replace(nameof(ShopBadges), view.ShopAdvice, s => $"{shopSize}{s.Slot}{s.Score}{s.Best}{s.Reason}{s.Placement:0.000}", () => ShopBadges = view.ShopAdvice
            .Where(s => s.Best || s.Score >= StrongPick)
            .Select(s => new ShopBadge(
                ShopRowCenter + ((s.Slot - ((shopSize - 1) / 2.0)) * ShopMinionPitch) - (ShopBadgeWidth / 2),
                ShortReason(s.Reason),
                s.Best,
                s.Placement is { } p ? $"{p.ToString("0.00", CultureInfo.CurrentCulture)} avg place" : string.Empty))
            .ToList());

        if (SelectedTier > 0 && _fingerprints.GetValueOrDefault("BrowserRaces") != string.Join(',', view.Races))
        {
            RebuildBrowser();
        }
    }

    private void UpdateLobbyAndSession(BattlegroundsView view, LiveSnapshot snapshot)
    {
        var rows = view.Opponents
            .Select(o => new LobbyRow(
                o.Place,
                o.HeroCardId,
                o.HeroName,
                o.Health + o.Armor,
                0,
                o.TechLevel,
                o.Triples,
                o.LastResult switch { "Win" => $"W {o.LastDamage}", "Loss" => $"L {o.LastDamage}", "Tie" => "T", _ => string.Empty },
                o.LastResult ?? string.Empty,
                o.IsNextOpponent,
                o.Dead,
                false))
            .ToList();
        if (view.HeroCardId is { } hero)
        {
            rows.Add(new LobbyRow(view.Place, hero, _cards.NameOf(hero), view.Health + view.Armor, 0, view.TechLevel, view.Triples,
                string.Empty, string.Empty, false, view.Health <= 0, true));
        }

        // Empty during hero selection: no opponents seen and no hero picked yet.
        var top = Math.Max(1, rows.Select(r => r.Health).DefaultIfEmpty(0).Max());
        Replace(nameof(LobbyRows), rows, r => $"{r.Place}{r.HeroCardId}{r.Health}{r.Tier}{r.Triples}{r.Last}{r.Next}{r.Dead}", () => LobbyRows = rows
            .Select(r => r with { HealthFraction = r.Dead ? 0 : Math.Clamp(r.Health / (double)top, 0.04, 1) })
            .OrderBy(r => r.Place <= 0 ? 99 : r.Place)
            .ToList());

        var session = snapshot.Session;
        SessionRating = session.StartRating is { } start && session.CurrentRating is { } now
            ? $"{start:N0} → {now:N0}"
            : Rating;
        var cut = view.IsDuos ? 2 : 4;
        Replace(nameof(SessionPlaces), session.Placements, p => p.ToString(CultureInfo.InvariantCulture), () =>
            SessionPlaces = session.Placements.TakeLast(12).Select(p => new PlaceChip(p, p <= cut)).ToList());

        var tribes = view.Board
            .SelectMany(m => _cards.Get(m.CardId)?.Races ?? Array.Empty<string>())
            .GroupBy(r => r)
            .OrderByDescending(g => g.Count())
            .Select(g => $"{Races.Display(g.Key, _locale)} ×{g.Count()}")
            .ToList();
        Replace(nameof(BoardTribes), tribes, t => t, () => BoardTribes = tribes);
    }

    // Tavern row geometry in 1080-unit overlay space (HDT: minion 0.12H plus a 0.0039H margin each side).
    public const double ShopRowWidth = 7 * ShopMinionPitch;
    public const double ShopBadgeWidth = 126;
    private const double ShopMinionPitch = 138;
    private const double ShopRowCenter = ShopRowWidth / 2;
    private const int StrongPick = 40;

    private static readonly string[] ReasonTags = ["Triple", "Pair", "Core", "Add-on", "Cycle", "Fits tribe", "Board synergy"];

    /// <summary>Badge text from the advisor's reason: its tags only, without the comp name.</summary>
    private static string ShortReason(string reason)
    {
        var parts = reason.Split(" · ");
        var tags = parts.Where(p => ReasonTags.Contains(p)).Take(2).ToList();
        if (tags.Count == 0 && parts.Contains("Best stats available"))
        {
            tags.Add("Best stats");
        }

        return string.Join(" · ", tags);
    }

    private CompRow Row(CompAdvice c) => new(
        c.Id,
        c.Name,
        string.IsNullOrEmpty(c.PowerLevel) ? "–" : c.PowerLevel,
        c.AveragePlacement.ToString("0.00", CultureInfo.CurrentCulture),
        c.HeroPlacement is { } h ? h.ToString("0.00", CultureInfo.CurrentCulture) : "—",
        c.Difficulty,
        string.Join(" · ", c.Tribes.Select(t => Races.Display(t, _locale))),
        c.OwnedCore,
        c.Cards.Count(x => x.Status == "CORE"),
        c.IsTarget,
        c.IsPinned,
        c.Why,
        string.Join("  ·  ", c.LookFor));

    private void RebuildTiers()
    {
        if (_bg is not { } bg)
        {
            return;
        }

        var max = Math.Max(6, bg.MaxTechLevel);
        Tiers = Enumerable.Range(1, max)
            .Select(t => new TierButton(t, t == bg.TechLevel, t == SelectedTier, t > 6 && bg.MaxTechLevel < 7))
            .ToList();
    }

    private void RebuildBrowser()
    {
        if (_bg is not { } bg || SelectedTier == 0)
        {
            Browser = Array.Empty<BrowserGroup>();
            return;
        }

        _fingerprints["BrowserRaces"] = string.Join(',', bg.Races);
        var lobby = bg.Races.ToHashSet(StringComparer.Ordinal);
        var target = bg.Comps.FirstOrDefault(c => c.IsTarget);
        var inComp = target?.Cards.ToDictionary(c => c.CardId, c => c.Status) ?? new Dictionary<string, string>();

        var minions = _cards.BattlegroundsMinions
            .Where(c => c.TechLevel == SelectedTier && (bg.IsDuos || !c.IsBattlegroundsDuosExclusive))
            .Where(c => lobby.Count == 0 || c.Races.Count == 0 || c.Races.Any(r => r == "ALL" || lobby.Contains(r)))
            .ToList();

        // Before the lobby is known, show every tribe in the pool.
        var order = lobby.Count > 0
            ? bg.Races.ToList()
            : minions.SelectMany(c => c.Races).Where(r => r != "ALL").Distinct().OrderBy(r => r, StringComparer.Ordinal).ToList();
        var known = order.ToHashSet(StringComparer.Ordinal);
        var groups = new List<BrowserGroup>();
        foreach (var race in order.Append("ALL").Append(string.Empty))
        {
            var cards = minions
                .Where(c => race.Length == 0 ? c.Races.Count == 0 : race == "ALL" ? c.Races.Contains("ALL") : c.Races.Contains(race) && !c.Races.Contains("ALL"))
                .Where(c => race.Length == 0 || race == "ALL" || c.Races.First(known.Contains) == race)
                .OrderBy(c => c.Name, StringComparer.CurrentCulture)
                .Select(c => new BrowserCard(c.Id, c.Name, inComp.ContainsKey(c.Id), inComp.GetValueOrDefault(c.Id)))
                .ToList();
            if (cards.Count > 0)
            {
                var title = race.Length == 0 ? "Neutral" : race == "ALL" ? "All tribes" : Races.Display(race, _locale);
                groups.Add(new BrowserGroup(title, cards));
            }
        }

        var spells = _cards.BattlegroundsSpells.Where(c => c.TechLevel == SelectedTier).OrderBy(c => c.Name, StringComparer.CurrentCulture)
            .Select(c => new BrowserCard(c.Id, c.Name, false, null)).ToList();
        if (spells.Count > 0)
        {
            groups.Add(new BrowserGroup("Tavern spells", spells));
        }

        Browser = groups;
    }

    /// <summary>Updates the leaderboard hover popup; <paramref name="slot"/> is 1..8 or 0 for none.</summary>
    public void HoverLeaderboard(int slot, double top, double left) =>
        ShowLobbyPopup(slot > 0 ? _bg?.Opponents.FirstOrDefault(o => o.Place == slot) : null, top, left);

    /// <summary>Updates the popup for the hero the game reports under the mouse; null clears it.</summary>
    public void HoverLeaderboardHero(string? heroCardId, double top, double left)
    {
        BgOpponent? opponent = null;
        if (!string.IsNullOrEmpty(heroCardId) && _bg is { } bg)
        {
            opponent = bg.Opponents.FirstOrDefault(o => o.HeroCardId == heroCardId)
                       ?? bg.Opponents.FirstOrDefault(o => BaseHero(o.HeroCardId) == BaseHero(heroCardId));
        }

        ShowLobbyPopup(opponent, top, left);
    }

    /// <summary>Hero id without a skin suffix, so a skinned portrait still matches its hero.</summary>
    private static string BaseHero(string cardId)
    {
        var skin = cardId.IndexOf("_SKIN", StringComparison.OrdinalIgnoreCase);
        return skin > 0 ? cardId[..skin] : cardId;
    }

    private void ShowLobbyPopup(BgOpponent? opponent, double top, double left)
    {
        if (opponent is null)
        {
            Lobby = null;
            return;
        }

        LobbyTop = top;
        LobbyLeft = left;
        if (Lobby?.HeroCardId == opponent.HeroCardId && Lobby.Board.Count == opponent.LastBoard.Count && Lobby.Health == opponent.Health)
        {
            return;
        }

        var turnsAgo = _bg!.Turn - opponent.LastBoardTurn;
        Lobby = new LobbyPopup(
            opponent.HeroCardId,
            opponent.HeroName,
            opponent.Health,
            opponent.Armor,
            opponent.TechLevel,
            opponent.Place,
            opponent.Triples,
            opponent.LastResult switch
            {
                null => string.Empty,
                "Win" => $"Last fight: you won, dealt {opponent.LastDamage}",
                "Loss" => $"Last fight: they won, you took {opponent.LastDamage}",
                var r => r,
            },
            opponent.LastBoard.Count == 0 ? "Board not seen yet" : turnsAgo <= 0 ? "Board this turn" : turnsAgo == 1 ? "Board 1 turn ago" : $"Board {turnsAgo} turns ago",
            opponent.IsNextOpponent,
            opponent.Dead,
            opponent.LastBoard.Select(m => new LobbyMinion(m.CardId, m.Name, m.Attack, m.Health, m.Golden, string.Join(" · ", m.Keywords))).ToList());
    }

    public void ShowOutcome(GameOutcome outcome)
    {
        var bg = outcome.Mode is "Battlegrounds" or "Duos";
        ToastTitle = bg ? $"{Ordinal(outcome.Placement)} place" : outcome.Result == "Win" ? "Victory" : outcome.Result == "Loss" ? "Defeat" : outcome.Result;
        ToastResult = bg ? (outcome.Placement <= (outcome.Mode == "Duos" ? 2 : 4) ? "Win" : "Loss") : outcome.Result;
        var hero = _cards.NameOf(outcome.HeroCardId);
        ToastDetail = outcome.RatingDelta is { } d
            ? $"{hero} · {(d >= 0 ? "+" : "−")}{Math.Abs(d)} rating → {outcome.RatingAfter:N0}"
            : hero;
        ShowToast = true;
    }

    private void Replace<T>(string key, IReadOnlyList<T> source, Func<T, string> fingerprint, Action apply)
    {
        var print = string.Join('|', source.Select(fingerprint));
        if (_fingerprints.TryGetValue(key, out var previous) && previous == print)
        {
            return;
        }

        _fingerprints[key] = print;
        apply();
    }

    public static string ClassName(string? cardClass) => string.IsNullOrEmpty(cardClass)
        ? string.Empty
        : cardClass switch
        {
            "DEMONHUNTER" => "Demon Hunter",
            "DEATHKNIGHT" => "Death Knight",
            _ => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(cardClass.ToLowerInvariant()),
        };

    private static string Ordinal(int n) => n switch
    {
        1 => "1st",
        2 => "2nd",
        3 => "3rd",
        _ => $"{n}th",
    };
}

