using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Hearthlens.UI.Infrastructure;

namespace Hearthlens.UI.Controls;

/// <summary>A soft wavy rule that fills its width (Material 3 Expressive divider).</summary>
public sealed class Wave : Control
{
    public static readonly StyledProperty<IBrush?> StrokeProperty = AvaloniaProperty.Register<Wave, IBrush?>(nameof(Stroke));

    public static readonly StyledProperty<double> AmplitudeProperty = AvaloniaProperty.Register<Wave, double>(nameof(Amplitude), 2.2);

    public static readonly StyledProperty<double> WavelengthProperty = AvaloniaProperty.Register<Wave, double>(nameof(Wavelength), 11.0);

    public static readonly StyledProperty<double> ThicknessProperty = AvaloniaProperty.Register<Wave, double>(nameof(Thickness), 1.5);

    static Wave()
    {
        AffectsRender<Wave>(StrokeProperty, AmplitudeProperty, WavelengthProperty, ThicknessProperty);
        AffectsMeasure<Wave>(AmplitudeProperty, ThicknessProperty);
    }

    public IBrush? Stroke { get => GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }

    public double Amplitude { get => GetValue(AmplitudeProperty); set => SetValue(AmplitudeProperty, value); }

    public double Wavelength { get => GetValue(WavelengthProperty); set => SetValue(WavelengthProperty, value); }

    public double Thickness { get => GetValue(ThicknessProperty); set => SetValue(ThicknessProperty, value); }

    protected override Size MeasureOverride(Size availableSize) =>
        new(double.IsInfinity(availableSize.Width) ? 40 : availableSize.Width, (Amplitude * 2) + Thickness + 1);

    public override void Render(DrawingContext context)
    {
        Draw(context, Stroke ?? ThemeBrushes.Get("Outline"), 0, Bounds.Width, Bounds.Height / 2, Amplitude, Wavelength, Thickness);
    }

    /// <summary>Draws a sine rule from <paramref name="from"/> to <paramref name="to"/> centred on <paramref name="y"/>.</summary>
    public static void Draw(DrawingContext context, IBrush stroke, double from, double to, double y, double amplitude, double wavelength, double thickness)
    {
        if (to - from < wavelength)
        {
            return;
        }

        var pen = new Pen(stroke, thickness, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            var inset = thickness;
            ctx.BeginFigure(new Point(from + inset, y), false);
            for (var x = from + inset; x <= to - inset; x += 1)
            {
                ctx.LineTo(new Point(x, y + (amplitude * Math.Sin((x - from) / wavelength * 2 * Math.PI))));
            }

            ctx.EndFigure(false);
        }

        context.DrawGeometry(null, pen, geometry);
    }
}
