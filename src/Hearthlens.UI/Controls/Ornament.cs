using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Hearthlens.UI.Infrastructure;

namespace Hearthlens.UI.Controls;

/// <summary>
/// Section heading in the Material 3 Expressive style: an optional icon inside a cookie shape, a bold title and a
/// wavy rule that fills the rest of the width.
/// </summary>
public sealed class Ornament : Control
{
    public static readonly StyledProperty<string> TextProperty = AvaloniaProperty.Register<Ornament, string>(nameof(Text), string.Empty);

    public static readonly StyledProperty<double> SizeProperty = AvaloniaProperty.Register<Ornament, double>(nameof(Size), 11.0);

    public static readonly StyledProperty<bool> CenteredProperty = AvaloniaProperty.Register<Ornament, bool>(nameof(Centered));

    public static readonly StyledProperty<string> IconProperty = AvaloniaProperty.Register<Ornament, string>(nameof(Icon), string.Empty);

    static Ornament()
    {
        AffectsRender<Ornament>(TextProperty, SizeProperty, CenteredProperty, IconProperty);
        AffectsMeasure<Ornament>(TextProperty, SizeProperty, IconProperty);
    }

    public string Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    public double Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    public bool Centered { get => GetValue(CenteredProperty); set => SetValue(CenteredProperty, value); }

    /// <summary>Material Design Icons glyph shown in a cookie before the title (empty for none).</summary>
    public string Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    private bool Large => Size >= 13;

    private double IconSize => Math.Round(Size * 2.3);

    /// <summary>"BATTLEGROUNDS RATING" reads as "Battlegrounds rating"; mixed-case text is kept.</summary>
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
            new Typeface(Font(Large ? "DisplayFont" : "UiFont"), FontStyle.Normal, FontWeight.Bold),
            Large ? Size + 3 : Size + 0.5, ThemeBrushes.Get(Large ? "Text" : "Muted"));

    protected override Size MeasureOverride(Size availableSize)
    {
        var text = Format();
        var iconWidth = Icon.Length > 0 ? IconSize + 10 : 0;
        var width = double.IsInfinity(availableSize.Width) ? iconWidth + text.Width + 48 : availableSize.Width;
        return new Size(width, Math.Max(Math.Ceiling(text.Height) + 4, Icon.Length > 0 ? IconSize : 0));
    }

    public override void Render(DrawingContext context)
    {
        var text = Format();
        var w = Bounds.Width;
        var mid = Bounds.Height / 2;
        var wave = ThemeBrushes.Get("Outline");
        var amplitude = Math.Max(1.6, Size * 0.17);
        var wavelength = Math.Max(8, Size * 0.85);

        if (Centered)
        {
            var left = (w - text.Width) / 2;
            context.DrawText(text, new Point(left, mid - (text.Height / 2)));
            Wave.Draw(context, wave, 0, left - 10, mid, amplitude, wavelength, 1.4);
            Wave.Draw(context, wave, left + text.Width + 10, w, mid, amplitude, wavelength, 1.4);
            return;
        }

        var x = 0.0;
        if (Icon.Length > 0)
        {
            var d = IconSize;
            using (context.PushTransform(Matrix.CreateTranslation(0, mid - (d / 2))))
            {
                context.DrawGeometry(ThemeBrushes.Get("Surface3"), null, Scallop.Build(d, d, 9, 0.09, 0));
            }

            var glyph = new FormattedText(Icon, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                new Typeface(Font("IconFont")), d * 0.5, ThemeBrushes.Get("Accent"));
            context.DrawText(glyph, new Point((d - glyph.Width) / 2, mid - (glyph.Height / 2)));
            x = d + 10;
        }

        context.DrawText(text, new Point(x, mid - (text.Height / 2)));
        Wave.Draw(context, wave, x + text.Width + 12, w, mid + 1, amplitude, wavelength, 1.4);
    }
}
