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
/// is auto-fitted to the data's own range; set <see cref="YMin"/> and <see cref="YMax"/>
/// to instead pin the vertical axis to a fixed range (e.g. 0–100 for a CPU % chart).
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

    public Sparkline() {
        InitializeComponent();
    }

    /// <summary>Line colour.</summary>
    public IBrush? Stroke {
        get => GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    /// <summary>Line thickness in the polyline's own coordinate space.</summary>
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
    }

    private void Rebuild() {
        var points = Parse(Points);

        // Fixed-range mode requires a valid, non-empty axis span.
        if (YMin is double lo && YMax is double hi && hi > lo) {
            FixedLine.Points.Clear();
            var maxX = 0.0;
            foreach (var p in points) {
                if (p.X > maxX)
                    maxX = p.X;
                // Offset by the axis floor so YMin sits at the Canvas top (small y = top).
                FixedLine.Points.Add(new Point(p.X, p.Y - lo));
            }

            // Size the Canvas to the domain so the Viewbox scales by the axis, not the data.
            FixedSurface.Width = maxX > 0 ? maxX : 1;
            FixedSurface.Height = hi - lo;

            FixedBox.IsVisible = true;
            AutoBox.IsVisible = false;
            return;
        }

        // Auto-fit mode: unchanged legacy behaviour.
        AutoLine.Points.Clear();
        foreach (var p in points)
            AutoLine.Points.Add(p);

        AutoBox.IsVisible = true;
        FixedBox.IsVisible = false;
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
