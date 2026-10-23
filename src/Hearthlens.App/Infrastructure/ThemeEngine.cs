using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;
using Hearthlens.Core;

namespace Hearthlens.App.Infrastructure;

/// <summary>
/// Builds the app's resource dictionary for the day or night edition. Themes/Theme.xaml is the day edition; for the
/// night edition its source (shipped inside the app) has every colour swapped through <see cref="ThemePalette.Night"/>.
/// Windows resolve their brushes when they are created, so the host recreates them after a change.
/// </summary>
public static partial class ThemeEngine
{
    private static readonly Uri BuiltIn = new("pack://application:,,,/Themes/Theme.xaml");
    private static string? _source;
    private static ResourceDictionary? _day;

    [GeneratedRegex("#([0-9A-Fa-f]{8}|[0-9A-Fa-f]{6})\\b")]
    private static partial Regex HexColor();

    [GeneratedRegex("clr-namespace:([\\w.]+)\"")]
    private static partial Regex LocalNamespace();

    public static void Apply(Application app, bool night)
    {
        var dictionary = night ? BuildNight() : new ResourceDictionary { Source = BuiltIn };
        var merged = app.Resources.MergedDictionaries;
        merged.Clear();
        merged.Add(dictionary);
    }

    /// <summary>A day-edition resource, for surfaces that stay in the day edition (the overlay's paper slips).</summary>
    public static object? DayResource(string key)
    {
        _day ??= new ResourceDictionary { Source = BuiltIn };
        return _day.Contains(key) ? _day[key] : null;
    }

    public static ResourceDictionary BuildNight()
    {
        _source ??= Load();
        var xaml = HexColor().Replace(_source, m => Recolor(m.Value));
        xaml = LocalNamespace().Replace(xaml, "clr-namespace:$1;assembly=Hearthlens\"");
        return (ResourceDictionary)XamlReader.Parse(xaml);
    }

    private static string Recolor(string hex)
    {
        var value = uint.Parse(hex.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        if (hex.Length == 7)
        {
            value |= 0xFF000000;
        }

        var mapped = ThemePalette.Night(value);
        return hex.Length == 7 ? $"#{mapped & 0xFFFFFF:X6}" : $"#{mapped:X8}";
    }

    private static string Load()
    {
        using var stream = typeof(ThemeEngine).Assembly.GetManifestResourceStream("Hearthlens.Theme.xaml")
            ?? throw new InvalidOperationException("Theme source is missing from the app.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}