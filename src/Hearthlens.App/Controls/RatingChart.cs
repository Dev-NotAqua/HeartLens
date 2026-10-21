using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Hearthlens.Core.Data;

namespace Hearthlens.App.Controls;

/// <summary>Rating over time: soft area, crisp line, min/max guides and the latest value.</summary>
public sealed class RatingChart : FrameworkElement
{
    public static readonly DependencyProperty PointsProperty = DependencyProperty.Register(
        nameof(Points), typeof(IReadOnlyList<RatingPoint>), typeof(RatingChart),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public IReadOnlyList<RatingPoint>? Points
    {
        get => (IReadOnlyList<RatingPoint>?)GetValue(PointsProperty);
        set => SetValue(PointsProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var w = ActualWidth;
        var h = ActualHeight;
        if (w < 40 || h < 40)
        {
            return;
        }

        var accent = (Brush)FindResource("Accent");
        var faint = (Brush)FindResource("Faint");
        var muted = (Brush)FindResource("Muted");
        var grid = new Pen((Brush)FindResource("Border"), 1) { DashStyle = new DashStyle([3, 4], 0) };
        var typeface = new Typeface(new FontFamily("Segoe UI Variable Text, Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, w, h));
        var points = Points;
        if (points is null || points.Count < 2)
        {
            var empty = new FormattedText("Play a few rated Battlegrounds games to see your rating curve.", CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, typeface, 12, faint, dpi);
            dc.DrawText(empty, new Point((w - empty.Width) / 2, (h - empty.Height) / 2));
            return;
        }

        const double left = 44;
        const double right = 12;
        const double top = 12;
        const double bottom = 10;
        var min = points.Min(p => p.Rating);
        var max = points.Max(p => p.Rating);
        var pad = Math.Max(20, (max - min) * 0.12);
        var lo = min - pad;
        var hi = max + pad;
        double X(int i) => left + ((w - left - right) * i / (points.Count - 1));
        double Y(int rating) => top + ((h - top - bottom) * (1 - ((rating - lo) / (hi - lo))));

        foreach (var value in new[] { max, min })
        {
            var y = Math.Round(Y(value)) + 0.5;
            dc.DrawLine(grid, new Point(left, y), new Point(w - right, y));
            var label = new FormattedText(value.ToString("N0", CultureInfo.CurrentCulture), CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, typeface, 11, muted, dpi);
            dc.DrawText(label, new Point(left - label.Width - 8, y - (label.Height / 2)));
        }

        var line = new StreamGeometry();
        using (var ctx = line.Open())
        {
            ctx.BeginFigure(new Point(X(0), Y(points[0].Rating)), false, false);
            for (var i = 1; i < points.Count; i++)
            {
                ctx.LineTo(new Point(X(i), Y(points[i].Rating)), true, true);
            }
        }

        line.Freeze();
        var area = new StreamGeometry();
        using (var ctx = area.Open())
        {
            ctx.BeginFigure(new Point(X(0), h - bottom), true, true);
            for (var i = 0; i < points.Count; i++)
            {
                ctx.LineTo(new Point(X(i), Y(points[i].Rating)), false, true);
            }

            ctx.LineTo(new Point(X(points.Count - 1), h - bottom), false, false);
        }

        area.Freeze();
        var gold = accent is SolidColorBrush solid ? solid.Color : Color.FromRgb(0xE9, 0xB9, 0x49);
        var fill = new LinearGradientBrush(Color.FromArgb(0x1F, gold.R, gold.G, gold.B), Color.FromArgb(0x00, gold.R, gold.G, gold.B), 90);
        dc.DrawGeometry(fill, null, area);
        dc.DrawGeometry(null, new Pen(accent, 2) { LineJoin = PenLineJoin.Round }, line);

        var last = new Point(X(points.Count - 1), Y(points[^1].Rating));
        dc.DrawEllipse((Brush)FindResource("Bg"), new Pen(accent, 2), last, 4, 4);
    }
}
