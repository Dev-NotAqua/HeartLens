using System.Collections;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Styling;

namespace Hearthlens.UI.Infrastructure;

/// <summary>Looks up theme brushes by key from the running application.</summary>
public static class ThemeBrushes
{
    // Converters and code-behind pick a brush once, so the app-theme brushes handed out are recoloured in place
    // when the theme changes instead of keeping the old edition's colour until the row is rebuilt.
    private static readonly Dictionary<string, SolidColorBrush> Live = new();
    private static bool _watching;

    /// <summary>The brush for the app's current theme. It follows later theme changes.</summary>
    public static IBrush Get(string key)
    {
        if (Application.Current is not { } app)
        {
            return Brushes.Gray;
        }

        Watch(app);
        if (Live.TryGetValue(key, out var live))
        {
            return live;
        }

        if (Lookup(app, key, app.ActualThemeVariant) is not ISolidColorBrush solid)
        {
            return Lookup(app, key, app.ActualThemeVariant);
        }

        live = new SolidColorBrush(solid.Color, solid.Opacity);
        Live[key] = live;
        return live;
    }

    /// <summary>The brush for a fixed theme, for surfaces that don't follow the app theme (the overlay is always light).</summary>
    public static IBrush Get(string key, ThemeVariant? variant) =>
        variant is null ? Get(key)
        : Application.Current is { } app ? Lookup(app, key, variant)
        : Brushes.Gray;

    private static IBrush Lookup(Application app, string key, ThemeVariant variant) =>
        app.TryGetResource(key, variant, out var value) && value is IBrush brush ? brush : Brushes.Gray;

    private static void Watch(Application app)
    {
        if (_watching)
        {
            return;
        }

        _watching = true;
        app.ActualThemeVariantChanged += (_, _) =>
        {
            foreach (var (key, brush) in Live)
            {
                if (Lookup(app, key, app.ActualThemeVariant) is ISolidColorBrush solid)
                {
                    brush.Color = solid.Color;
                    brush.Opacity = solid.Opacity;
                }
            }
        };
    }
}

/// <summary>True when the value is truthy: true, non-zero, non-empty, non-null. Parameter "!" inverts.</summary>
public sealed class Truthy : IValueConverter
{
    public static bool Test(object? value) => value switch
    {
        null => false,
        bool b => b,
        int i => i != 0,
        double d => d != 0,
        string s => s.Length > 0,
        ICollection c => c.Count > 0,
        _ => true,
    };

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Test(value) ^ (parameter as string == "!");

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Maps keys (tier letters, rarities, results) to theme brushes.</summary>
public sealed class KeyBrush : IValueConverter
{
    /// <summary>Fixed theme to colour for; null follows the app theme.</summary>
    public ThemeVariant? Variant { get; set; }

    public static string KeyFor(string? value) => value switch
    {
        "S" => "TierS",
        "A" => "TierA",
        "B" => "TierB",
        "C" => "TierC",
        "D" => "TierD",
        "COMMON" or "FREE" => "RarityCommon",
        "RARE" => "RarityRare",
        "EPIC" => "RarityEpic",
        "LEGENDARY" => "RarityLegendary",
        "Win" or "Won" or "CORE" => "Positive",
        "Loss" or "Lost" => "Negative",
        "ADDON" => "Info",
        _ => "Muted",
    };

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        ThemeBrushes.Get(KeyFor(value?.ToString()), Variant);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>
/// Placement (1-8) or signed number to positive/negative/muted brush. A numeric parameter is the top-N cutoff for
/// placements; "text" makes zero use the normal text colour instead of muted.
/// </summary>
public sealed class SignBrush : IValueConverter
{
    /// <summary>Fixed theme to colour for; null follows the app theme.</summary>
    public ThemeVariant? Variant { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var zero = parameter as string == "text" ? "Text" : "Muted";
        var key = zero;
        if (parameter is string cut && int.TryParse(cut, out var top) && value is int place)
        {
            key = place <= 0 ? "Muted" : place <= top ? "Positive" : "Negative";
        }
        else if (value is IConvertible c)
        {
            var d = c.ToDouble(CultureInfo.InvariantCulture);
            key = d > 0 ? "Positive" : d < 0 ? "Negative" : zero;
        }

        return ThemeBrushes.Get(key, Variant);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>0..1 fraction to a width; parameter is the full width.</summary>
public sealed class FractionWidth : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var full = double.TryParse(parameter as string, NumberStyles.Float, CultureInfo.InvariantCulture, out var w) ? w : 100;
        var fraction = value is IConvertible c ? Math.Clamp(c.ToDouble(CultureInfo.InvariantCulture), 0, 1) : 0;
        return Math.Max(fraction * full, 0);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>A number to a left margin, for items placed at an x offset inside an overlapping panel.</summary>
public sealed class LeftMargin : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        new Thickness(value is IConvertible c ? c.ToDouble(CultureInfo.InvariantCulture) : 0, 0, 0, 0);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
