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

    private List<Point> _data = new();
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

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change) {
        base.OnPropertyChanged(change);
        if (change.Property == PointsProperty
            || change.Property == YMinProperty
            || change.Property == YMaxProperty)
            Rebuild();
        else if (_fixedRange
            && (change.Property == StrokeProperty || change.Property == StrokeThicknessProperty))
            InvalidateVisual();
    }

    public override void Render(DrawingContext context) {
        base.Render(context);
        if (!_fixedRange || _data.Count < 2 || Stroke is null)
            return;

        double w = Bounds.Width, h = Bounds.Height;
        if (w <= 0 || h <= 0)
            return;

        var maxX = 0.0;
        foreach (var p in _data)
            if (p.X > maxX)
                maxX = p.X;

        var span = _yMax - _yMin;
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open()) {
            var first = true;
            foreach (var p in _data) {
                // x across the width; y keeps "smaller = top" so the axis floor is at the top.
                var px = maxX > 0 ? p.X / maxX * w : 0;
                var py = (p.Y - _yMin) / span * h;
                var point = new Point(px, py);
                if (first) {
                    ctx.BeginFigure(point, isFilled: false);
                    first = false;
                } else {
                    ctx.LineTo(point);
                }
            }

            ctx.EndFigure(isClosed: false);
        }

        var pen = new Pen(Stroke, StrokeThickness) {
            LineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round,
        };
        context.DrawGeometry(null, pen, geometry);
    }

    private void Rebuild() {
        _data = Parse(Points);
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
