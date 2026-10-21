using System.Net.Http;
using System.Text.Json;

namespace Hearthlens.Core.Cards;

/// <summary>Card metadata from HearthstoneJSON, cached on disk per locale.</summary>
public sealed class CardDb
{
    private static readonly TimeSpan MaxAge = TimeSpan.FromDays(2);

    private readonly Dictionary<string, Card> _byId;
    private readonly Dictionary<int, Card> _byDbf;

    private CardDb(string locale, IEnumerable<Card> cards)
    {
        Locale = locale;
        _byId = new Dictionary<string, Card>(StringComparer.OrdinalIgnoreCase);
        _byDbf = new Dictionary<int, Card>();
        foreach (var card in cards)
        {
            _byId[card.Id] = card;
            if (card.DbfId > 0)
            {
                _byDbf[card.DbfId] = card;
            }
        }

        BattlegroundsMinions = _byId.Values
            .Where(c => c.IsBattlegroundsPoolMinion && c.TechLevel > 0)
            .OrderBy(c => c.TechLevel).ThenBy(c => c.Name, StringComparer.CurrentCulture)
            .ToList();
        BattlegroundsSpells = _byId.Values
            .Where(c => c.IsBattlegroundsPoolSpell && c.TechLevel > 0)
            .OrderBy(c => c.TechLevel).ThenBy(c => c.Name, StringComparer.CurrentCulture)
            .ToList();
    }

    public static CardDb Empty { get; } = new("enUS", Array.Empty<Card>());

    public string Locale { get; }

    public int Count => _byId.Count;

    public IReadOnlyList<Card> BattlegroundsMinions { get; }

    public IReadOnlyList<Card> BattlegroundsSpells { get; }

    public Card? Get(string? cardId) => cardId is { Length: > 0 } && _byId.TryGetValue(cardId, out var card) ? card : null;

    public Card? Get(int dbfId) => _byDbf.GetValueOrDefault(dbfId);

    public string NameOf(string? cardId) => Get(cardId)?.Name ?? cardId ?? string.Empty;

    /// <summary>Loads from cache, refreshing from the network when stale. Never throws.</summary>
    public static async Task<CardDb> LoadAsync(string locale, HttpClient http, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(AppPaths.Cache, $"cards.{locale}.json");
        var file = new FileInfo(path);
        if (!file.Exists || DateTime.UtcNow - file.LastWriteTimeUtc > MaxAge)
        {
            try
            {
                var url = $"https://api.hearthstonejson.com/v1/latest/{locale}/cards.json";
                var bytes = await http.GetByteArrayAsync(url, cancellationToken).ConfigureAwait(false);
                var temp = path + ".tmp";
                await File.WriteAllBytesAsync(temp, bytes, cancellationToken).ConfigureAwait(false);
                File.Move(temp, path, overwrite: true);
                Diag.Info($"Card data refreshed ({locale}, {bytes.Length:N0} bytes)");
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException)
            {
                Diag.Error("Card data download failed", ex);
            }
        }

        if (!File.Exists(path))
        {
            return Empty;
        }

        try
        {
            await using var stream = File.OpenRead(path);
            return Parse(locale, stream);
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            Diag.Error("Card data unreadable", ex);
            return Empty;
        }
    }

    public static CardDb Parse(string locale, Stream json)
    {
        using var doc = JsonDocument.Parse(json);
        var cards = new List<Card>(doc.RootElement.GetArrayLength());
        foreach (var e in doc.RootElement.EnumerateArray())
        {
            var id = Str(e, "id");
            if (id.Length == 0)
            {
                continue;
            }

            cards.Add(new Card
            {
                Id = id,
                DbfId = Int(e, "dbfId"),
                Name = Str(e, "name"),
                Text = Str(e, "text"),
                Cost = Int(e, "cost"),
                Attack = Int(e, "attack"),
                Health = Int(e, "health"),
                Armor = Int(e, "armor"),
                Type = Str(e, "type"),
                Rarity = Str(e, "rarity"),
                CardClass = Str(e, "cardClass"),
                Set = Str(e, "set"),
                Races = StrArray(e, "races") is { Count: > 0 } races ? races : Str(e, "race") is { Length: > 0 } race ? [race] : Array.Empty<string>(),
                Mechanics = StrArray(e, "mechanics"),
                Collectible = Bool(e, "collectible"),
                TechLevel = Int(e, "techLevel"),
                IsBattlegroundsPoolMinion = Bool(e, "isBattlegroundsPoolMinion"),
                IsBattlegroundsPoolSpell = Bool(e, "isBattlegroundsPoolSpell"),
                IsBattlegroundsHero = Bool(e, "battlegroundsHero"),
                IsBattlegroundsDuosExclusive = Bool(e, "isBattlegroundsDuosExclusive"),
                BattlegroundsPremiumDbfId = Int(e, "battlegroundsPremiumDbfId"),
                BattlegroundsNormalDbfId = Int(e, "battlegroundsNormalDbfId"),
                BattlegroundsSkinParentId = Int(e, "battlegroundsSkinParentId"),
            });
        }

        return new CardDb(locale, cards);
    }

    /// <summary>Maps a hero skin to its base hero so stats aggregate across skins.</summary>
    public string BaseHeroId(string cardId)
    {
        var card = Get(cardId);
        if (card is { BattlegroundsSkinParentId: > 0 } && Get(card.BattlegroundsSkinParentId) is { } parent)
        {
            return parent.Id;
        }

        var skin = cardId.IndexOf("_SKIN_", StringComparison.Ordinal);
        return skin > 0 ? cardId[..skin] : cardId;
    }

    private static string Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? string.Empty : string.Empty;

    private static int Int(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i) ? i : 0;

    private static bool Bool(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;

    private static IReadOnlyList<string> StrArray(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out var v) || v.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        var list = new List<string>(v.GetArrayLength());
        foreach (var item in v.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String && item.GetString() is { } s)
            {
                list.Add(s);
            }
        }

        return list;
    }
}
