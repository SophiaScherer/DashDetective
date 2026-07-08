using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace DashDetective.Shared.Controls;

/// <summary>
/// A reusable sparkline / line chart. Points are supplied as a whitespace separated
/// "x,y" string. By default the internal <see cref="Avalonia.Controls.Shapes.Polyline"/>
/// (inside a Viewbox) is auto-fitted to the data's own range; set <see cref="YMin"/> and
/// <see cref="YMax"/> to instead pin the vertical axis to a fixed range (e.g. 0–100 for a
/// CPU % chart), in which case the line is drawn directly in <see cref="Render"/>.
///
/// In fixed-range mode an optional second series (<see cref="Points2"/>/<see cref="Stroke2"/>)
/// may be supplied; both series share the same <see cref="YMin"/>/<see cref="YMax"/> axis so
/// their values are directly comparable. Set <see cref="Fill"/> to draw a translucent gradient
/// area beneath each line. These extras apply only to fixed-range mode; auto-fit mode is
/// unchanged single-series behaviour.
/// </summary>
public partial class Sparkline : UserControl {
    public static readonly StyledProperty<IBrush?> StrokeProperty =
        AvaloniaProperty.Register<Sparkline, IBrush?>(nameof(Stroke));

    public static readonly StyledProperty<double> StrokeThicknessProperty =
        AvaloniaProperty.Register<Sparkline, double>(nameof(StrokeThickness), 1.6);

    public static readonly StyledProperty<string?> PointsProperty =
        AvaloniaProperty.Register<Sparkline, string?>(nameof(Points));

    public static readonly StyledProperty<double?> YMinProperty =
        AvaloniaProperty.Register<Sparkline, double?>(nameof(YMin));

    public static readonly StyledProperty<double?> YMaxProperty =
        AvaloniaProperty.Register<Sparkline, double?>(nameof(YMax));

    public static readonly StyledProperty<string?> Points2Property =
        AvaloniaProperty.Register<Sparkline, string?>(nameof(Points2));

    public static readonly StyledProperty<IBrush?> Stroke2Property =
        AvaloniaProperty.Register<Sparkline, IBrush?>(nameof(Stroke2));

    public static readonly StyledProperty<bool> FillProperty =
        AvaloniaProperty.Register<Sparkline, bool>(nameof(Fill));

    private List<Point> _data = new();
    private List<Point> _data2 = new();
    private bool _fixedRange;
    private double _yMin, _yMax;

    public Sparkline() {
        InitializeComponent();
    }

    /// <summary>Line colour.</summary>
    public IBrush? Stroke {
        get => GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    /// <summary>Line thickness in pixels.</summary>
    public double StrokeThickness {
        get => GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    /// <summary>Points as a "x,y x,y …" string (any consistent coordinate range).</summary>
    public string? Points {
        get => GetValue(PointsProperty);
        set => SetValue(PointsProperty, value);
    }

    /// <summary>Optional lower bound of the vertical axis. Set with <see cref="YMax"/> to fix the scale.</summary>
    public double? YMin {
        get => GetValue(YMinProperty);
        set => SetValue(YMinProperty, value);
    }

    /// <summary>Optional upper bound of the vertical axis. Set with <see cref="YMin"/> to fix the scale.</summary>
    public double? YMax {
        get => GetValue(YMaxProperty);
        set => SetValue(YMaxProperty, value);
    }

    /// <summary>Optional second series ("x,y x,y …"), drawn on the same fixed axis as <see cref="Points"/>.</summary>
    public string? Points2 {
        get => GetValue(Points2Property);
        set => SetValue(Points2Property, value);
    }

    /// <summary>Line colour for the second series.</summary>
    public IBrush? Stroke2 {
        get => GetValue(Stroke2Property);
        set => SetValue(Stroke2Property, value);
    }

    /// <summary>When true (fixed-range mode), draw a translucent gradient area beneath each line.</summary>
    public bool Fill {
        get => GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change) {
        base.OnPropertyChanged(change);
        if (change.Property == PointsProperty
            || change.Property == Points2Property
            || change.Property == YMinProperty
            || change.Property == YMaxProperty)
            Rebuild();
        else if (_fixedRange
            && (change.Property == StrokeProperty || change.Property == StrokeThicknessProperty
                || change.Property == Stroke2Property || change.Property == FillProperty))
            InvalidateVisual();
    }

    public override void Render(DrawingContext context) {
        base.Render(context);
        if (!_fixedRange)
            return;

        double w = Bounds.Width, h = Bounds.Height;
        if (w <= 0 || h <= 0)
            return;

        var hasSeries1 = _data.Count >= 2 && Stroke is not null;
        var hasSeries2 = _data2.Count >= 2 && Stroke2 is not null;
        if (!hasSeries1 && !hasSeries2)
            return;

        // Share the x scale across both series so equal indices line up horizontally.
        var maxX = 0.0;
        foreach (var p in _data)
            if (p.X > maxX) maxX = p.X;
        foreach (var p in _data2)
            if (p.X > maxX) maxX = p.X;

        var span = _yMax - _yMin;
        if (span <= 0)
            return;

        // Draw all fills first, then all lines on top, so no line is occluded by a fill.
        if (Fill) {
            if (hasSeries1)
                DrawArea(context, _data, Stroke, w, h, maxX, span);
            if (hasSeries2)
                DrawArea(context, _data2, Stroke2, w, h, maxX, span);
        }

        if (hasSeries1)
            DrawLine(context, _data, Stroke!, w, h, maxX, span);
        if (hasSeries2)
            DrawLine(context, _data2, Stroke2!, w, h, maxX, span);
    }

    /// <summary>Maps a data point to control pixels. Keeps "smaller y = top" so the axis floor is at the top.</summary>
    private Point ToPixel(Point p, double w, double h, double maxX, double span) {
        var px = maxX > 0 ? p.X / maxX * w : 0;
        var py = (p.Y - _yMin) / span * h;
        return new Point(px, py);
    }

    private void DrawLine(DrawingContext context, List<Point> data, IBrush stroke,
        double w, double h, double maxX, double span) {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open()) {
            var first = true;
            foreach (var p in data) {
                var point = ToPixel(p, w, h, maxX, span);
                if (first) {
                    ctx.BeginFigure(point, isFilled: false);
                    first = false;
                } else {
                    ctx.LineTo(point);
                }
            }
            ctx.EndFigure(isClosed: false);
        }

        var pen = new Pen(stroke, StrokeThickness) {
            LineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round,
        };
        context.DrawGeometry(null, pen, geometry);
    }

    private void DrawArea(DrawingContext context, List<Point> data, IBrush? stroke,
        double w, double h, double maxX, double span) {
        var fill = MakeAreaBrush(stroke);
        if (fill is null)
            return;

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open()) {
            var start = ToPixel(data[0], w, h, maxX, span);
            ctx.BeginFigure(new Point(start.X, h), isFilled: true); // start on the bottom axis
            ctx.LineTo(start);
            for (var i = 1; i < data.Count; i++)
                ctx.LineTo(ToPixel(data[i], w, h, maxX, span));
            var end = ToPixel(data[^1], w, h, maxX, span);
            ctx.LineTo(new Point(end.X, h)); // drop back to the bottom axis
            ctx.EndFigure(isClosed: true);
        }

        context.DrawGeometry(fill, null, geometry);
    }

    /// <summary>Vertical gradient (α 0.34 → 0.02) of the series colour for the area fill; null if colour unknown.</summary>
    private static IBrush? MakeAreaBrush(IBrush? stroke) {
        if (stroke is not ISolidColorBrush solid)
            return null;
        var c = solid.Color;
        return new LinearGradientBrush {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops = {
                new GradientStop(Color.FromArgb(87, c.R, c.G, c.B), 0),  // ~0.34 alpha
                new GradientStop(Color.FromArgb(5, c.R, c.G, c.B), 1),   // ~0.02 alpha
            },
        };
    }

    private void Rebuild() {
        _data = Parse(Points);
        _data2 = Parse(Points2);
        _fixedRange = YMin is double lo && YMax is double hi && hi > lo;

        if (_fixedRange) {
            _yMin = YMin!.Value;
            _yMax = YMax!.Value;
            AutoBox.IsVisible = false;
            InvalidateVisual();
            return;
        }

        // Auto-fit mode: unchanged legacy behaviour (Viewbox stretches the raw points).
        AutoLine.Points.Clear();
        foreach (var p in _data)
            AutoLine.Points.Add(p);
        AutoBox.IsVisible = true;
        InvalidateVisual();
    }

    private static List<Point> Parse(string? raw) {
        var result = new List<Point>();
        if (string.IsNullOrWhiteSpace(raw))
            return result;

        var tokens = raw.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var token in tokens) {
            var parts = token.Split(',');
            if (parts.Length == 2
                && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
                && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y)) {
                result.Add(new Point(x, y));
            }
        }

        return result;
    }
}
