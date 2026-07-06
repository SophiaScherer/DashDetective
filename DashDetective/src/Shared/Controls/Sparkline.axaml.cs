using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace DashDetective.Shared.Controls;

/// <summary>
/// A reusable sparkline / line chart. Points are supplied as a whitespace
/// separated "x,y" string and rendered by an internal <see cref="Avalonia.Controls.Shapes.Polyline"/>.
/// </summary>
public partial class Sparkline : UserControl {
    public static readonly StyledProperty<IBrush?> StrokeProperty =
        AvaloniaProperty.Register<Sparkline, IBrush?>(nameof(Stroke));

    public static readonly StyledProperty<double> StrokeThicknessProperty =
        AvaloniaProperty.Register<Sparkline, double>(nameof(StrokeThickness), 1.6);

    public static readonly StyledProperty<string?> PointsProperty =
        AvaloniaProperty.Register<Sparkline, string?>(nameof(Points));

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

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change) {
        base.OnPropertyChanged(change);
        if (change.Property == PointsProperty)
            RebuildPoints();
    }

    private void RebuildPoints() {
        Line.Points.Clear();
        if (string.IsNullOrWhiteSpace(Points))
            return;

        var tokens = Points.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var token in tokens) {
            var parts = token.Split(',');
            if (parts.Length == 2
                && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
                && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y)) {
                Line.Points.Add(new Point(x, y));
            }
        }
    }
}
