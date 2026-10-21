using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace Hearthlens.App.Controls;

/// <summary>
/// Section heading in the Material 3 Expressive style: an optional icon inside a cookie shape, a bold
/// title and a wavy rule that fills the rest of the width.
/// </summary>
public sealed class Ornament : FrameworkElement
{
    private static readonly FontFamily FallbackFont = new("Segoe UI Variable Text, Segoe UI");

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(Ornament),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
        nameof(Size), typeof(double), typeof(Ornament),
        new FrameworkPropertyMetadata(11.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty CenteredProperty = DependencyProperty.Register(
        nameof(Centered), typeof(bool), typeof(Ornament),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon), typeof(string), typeof(Ornament),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }

    public double Size { get => (double)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    public bool Centered { get => (bool)GetValue(CenteredProperty); set => SetValue(CenteredProperty, value); }

    /// <summary>Material Design Icons glyph shown in a cookie before the title (empty for none).</summary>
    public string Icon { get => (string)GetValue(IconProperty); set => SetValue(IconProperty, value); }

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

    private FormattedText Format()
    {
        var family = (Large ? TryFindResource("DisplayFont") : TryFindResource("UiFont")) as FontFamily ?? FallbackFont;
        var ink = TryFindResource(Large ? "Text" : "Muted") as Brush ?? Brushes.Gainsboro;
        var face = new Typeface(family, FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
        return new FormattedText(SentenceCase(Text ?? string.Empty), CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            face, Large ? Size + 3 : Size + 0.5, ink, VisualTreeHelper.GetDpi(this).PixelsPerDip);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var text = Format();
        var iconWidth = Icon.Length > 0 ? IconSize + 10 : 0;
        var width = double.IsInfinity(availableSize.Width) ? iconWidth + text.Width + 48 : availableSize.Width;
        return new Size(width, Math.Max(Math.Ceiling(text.Height) + 4, Icon.Length > 0 ? IconSize : 0));
    }

    protected override void OnRender(DrawingContext dc)
    {
        var text = Format();
        var w = ActualWidth;
        var mid = ActualHeight / 2;
        var wave = TryFindResource("Outline") as Brush ?? Brushes.DimGray;
        var amplitude = Math.Max(1.6, Size * 0.17);
        var wavelength = Math.Max(8, Size * 0.85);

        if (Centered)
        {
            var left = (w - text.Width) / 2;
            dc.DrawText(text, new Point(left, mid - (text.Height / 2)));
            Wave.Draw(dc, wave, 0, left - 10, mid, amplitude, wavelength, 1.4);
            Wave.Draw(dc, wave, left + text.Width + 10, w, mid, amplitude, wavelength, 1.4);
            return;
        }

        var x = 0.0;
        if (Icon.Length > 0)
        {
            var d = IconSize;
            var cookie = Scallop.Build(d, d, 9, 0.09, 0);
            dc.PushTransform(new TranslateTransform(0, mid - (d / 2)));
            dc.DrawGeometry(TryFindResource("Surface3") as Brush ?? Brushes.DimGray, null, cookie);
            dc.Pop();
            var glyph = new FormattedText(Icon, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                new Typeface((TryFindResource("IconFont") as FontFamily) ?? FallbackFont, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
                d * 0.5, TryFindResource("Accent") as Brush ?? Brushes.LightGreen, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(glyph, new Point((d - glyph.Width) / 2, mid - (glyph.Height / 2)));
            x = d + 10;
        }

        dc.DrawText(text, new Point(x, mid - (text.Height / 2)));
        Wave.Draw(dc, wave, x + text.Width + 12, w, mid + 1, amplitude, wavelength, 1.4);
    }
}
