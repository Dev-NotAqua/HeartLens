using System.Windows;
using System.Windows.Media;

namespace Hearthlens.App.Controls;

/// <summary>A soft wavy rule that fills its width (Material 3 Expressive divider).</summary>
public sealed class Wave : FrameworkElement
{
    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
        nameof(Stroke), typeof(Brush), typeof(Wave),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty AmplitudeProperty = DependencyProperty.Register(
        nameof(Amplitude), typeof(double), typeof(Wave),
        new FrameworkPropertyMetadata(2.2, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty WavelengthProperty = DependencyProperty.Register(
        nameof(Wavelength), typeof(double), typeof(Wave),
        new FrameworkPropertyMetadata(11.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ThicknessProperty = DependencyProperty.Register(
        nameof(Thickness), typeof(double), typeof(Wave),
        new FrameworkPropertyMetadata(1.5, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public Brush? Stroke { get => (Brush?)GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }

    public double Amplitude { get => (double)GetValue(AmplitudeProperty); set => SetValue(AmplitudeProperty, value); }

    public double Wavelength { get => (double)GetValue(WavelengthProperty); set => SetValue(WavelengthProperty, value); }

    public double Thickness { get => (double)GetValue(ThicknessProperty); set => SetValue(ThicknessProperty, value); }

    protected override Size MeasureOverride(Size availableSize) =>
        new(double.IsInfinity(availableSize.Width) ? 40 : availableSize.Width, (Amplitude * 2) + Thickness + 1);

    protected override void OnRender(DrawingContext dc)
    {
        var stroke = Stroke ?? TryFindResource("Outline") as Brush;
        if (stroke is not null)
        {
            Draw(dc, stroke, 0, ActualWidth, ActualHeight / 2, Amplitude, Wavelength, Thickness);
        }
    }

    /// <summary>Draws a sine rule from <paramref name="from"/> to <paramref name="to"/> centred on <paramref name="y"/>.</summary>
    public static void Draw(DrawingContext dc, Brush stroke, double from, double to, double y, double amplitude, double wavelength, double thickness)
    {
        if (to - from < wavelength)
        {
            return;
        }

        var pen = new Pen(stroke, thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        pen.Freeze();
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            var inset = thickness;
            ctx.BeginFigure(new Point(from + inset, y), false, false);
            for (var x = from + inset; x <= to - inset; x += 1)
            {
                ctx.LineTo(new Point(x, y + (amplitude * Math.Sin((x - from) / wavelength * 2 * Math.PI))), true, true);
            }
        }

        geometry.Freeze();
        dc.DrawGeometry(null, pen, geometry);
    }
}
