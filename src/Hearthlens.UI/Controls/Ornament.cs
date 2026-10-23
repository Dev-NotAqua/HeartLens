using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Hearthlens.UI.Infrastructure;

namespace Hearthlens.UI.Controls;

/// <summary>A section heading: an optional quiet icon, the title, and a hairline that takes the rest of the row.</summary>
public sealed class Ornament : Control
{
    public static readonly StyledProperty<string> TextProperty = AvaloniaProperty.Register<Ornament, string>(nameof(Text), string.Empty);

    public static readonly StyledProperty<double> SizeProperty = AvaloniaProperty.Register<Ornament, double>(nameof(Size), 11.0);

    public static readonly StyledProperty<bool> CenteredProperty = AvaloniaProperty.Register<Ornament, bool>(nameof(Centered));

    public static readonly StyledProperty<string> IconProperty = AvaloniaProperty.Register<Ornament, string>(nameof(Icon), string.Empty);

    static Ornament()
    {
        AffectsRender<Ornament>(TextProperty, SizeProperty, CenteredProperty, IconProperty, ThemeVariantScope.ActualThemeVariantProperty);
        AffectsMeasure<Ornament>(TextProperty, SizeProperty, IconProperty);
    }

    public string Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    public double Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    public bool Centered { get => GetValue(CenteredProperty); set => SetValue(CenteredProperty, value); }

    /// <summary>Material Design Icons glyph shown before the title (empty for none).</summary>
    public string Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    private bool Large => Size >= 13;

    private double IconSize => Math.Round(Size * 1.45);

    private static string SentenceCase(string text)
    {
        if (text.Length == 0 || text.Any(char.IsLower))
        {
            return text;
        }

        var lower = text.ToLower(CultureInfo.CurrentCulture);
        return char.ToUpper(lower[0], CultureInfo.CurrentCulture) + lower[1..];
    }

    private FontFamily Font(string key) =>
        this.TryFindResource(key, ActualThemeVariant, out var value) && value is FontFamily family ? family : FontFamily.Default;

    private FormattedText Format() =>
        new(SentenceCase(Text ?? string.Empty), CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(Font(Large ? "DisplayFont" : "UiFont"), FontStyle.Normal, Large ? FontWeight.SemiBold : FontWeight.Medium),
            Large ? Size + 3 : Size + 0.5, ThemeBrushes.Get(Large ? "Text" : "Muted", ActualThemeVariant));

    protected override Size MeasureOverride(Size availableSize)
    {
        var text = Format();
        var iconWidth = Icon.Length > 0 ? IconSize + 7 : 0;
        var width = double.IsInfinity(availableSize.Width) ? iconWidth + text.Width + 40 : availableSize.Width;
        return new Size(width, Math.Max(Math.Ceiling(text.Height) + 2, IconSize));
    }

    public override void Render(DrawingContext context)
    {
        var text = Format();
        var width = Bounds.Width;
        var mid = Bounds.Height / 2;
        var rule = ThemeBrushes.Get("GoldTrim", ActualThemeVariant);

        if (Centered)
        {
            var left = (width - text.Width) / 2;
            context.DrawText(text, new Point(left, mid - (text.Height / 2)));
            DrawRule(context, rule, 0, left - 12, mid);
            DrawRule(context, rule, left + text.Width + 12, width, mid);
            return;
        }

        var x = 0.0;
        if (Icon.Length > 0)
        {
            var glyph = new FormattedText(Icon, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                new Typeface(Font("IconFont")), IconSize, ThemeBrushes.Get("Accent", ActualThemeVariant));
            context.DrawText(glyph, new Point(0, mid - (glyph.Height / 2)));
            x = IconSize + 7;
        }

        context.DrawText(text, new Point(x, mid - (text.Height / 2)));
        DrawRule(context, rule, x + text.Width + 12, width, mid);
    }

    private static void DrawRule(DrawingContext context, IBrush brush, double from, double to, double y)
    {
        if (to - from < 8)
        {
            return;
        }

        var line = Math.Floor(y) + 0.5;
        context.DrawLine(new Pen(brush, 1), new Point(from, line), new Point(to, line));
    }
}
