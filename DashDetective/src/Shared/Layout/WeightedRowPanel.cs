using Avalonia;
using Avalonia.Controls;
using System;
using System.Collections.Generic;

namespace DashDetective.Shared.Layout;

/// <summary>
/// A row of weighted, heterogeneous panels. Divides its width by each child's <see cref="WeightProperty"/>
/// while every slice still clears that child's own <c>MinWidth</c>; below that it stacks them
/// vertically at full width. Replaces a <c>Grid</c> with fixed star columns, which keeps shrinking
/// its panels instead of reflowing. The threshold comes from <see cref="WeightedRowLayout"/>.
///
/// Minimums are read from each child's own <c>MinWidth</c> rather than a parallel attached property,
/// so there is one source of truth and Avalonia keeps honouring it in the child's own measure.
/// </summary>
public class WeightedRowPanel : Panel {
    /// <summary>This child's share of the row. Defaults to 1, an equal split.</summary>
    public static readonly AttachedProperty<double> WeightProperty =
        AvaloniaProperty.RegisterAttached<WeightedRowPanel, Control, double>("Weight", 1.0);

    public static readonly StyledProperty<double> ColumnSpacingProperty =
        AvaloniaProperty.Register<WeightedRowPanel, double>(nameof(ColumnSpacing));

    public static readonly StyledProperty<double> RowSpacingProperty =
        AvaloniaProperty.Register<WeightedRowPanel, double>(nameof(RowSpacing));

    private readonly List<Control> _visible = new();
    private double[] _weights = Array.Empty<double>();
    private double[] _minWidths = Array.Empty<double>();
    private double[] _widths = Array.Empty<double>();
    private bool _stacked;

    static WeightedRowPanel() {
        AffectsParentMeasure<WeightedRowPanel>(WeightProperty);
        AffectsMeasure<WeightedRowPanel>(ColumnSpacingProperty, RowSpacingProperty);
    }

    /// <summary>Horizontal gap between panels while the row fits.</summary>
    public double ColumnSpacing {
        get => GetValue(ColumnSpacingProperty);
        set => SetValue(ColumnSpacingProperty, value);
    }

    /// <summary>Vertical gap between panels once the row has stacked.</summary>
    public double RowSpacing {
        get => GetValue(RowSpacingProperty);
        set => SetValue(RowSpacingProperty, value);
    }

    public static double GetWeight(Control control) => control.GetValue(WeightProperty);

    public static void SetWeight(Control control, double value) => control.SetValue(WeightProperty, value);

    protected override Size MeasureOverride(Size availableSize) {
        CollectVisible();
        var count = _visible.Count;
        if (count == 0) {
            _stacked = false;
            return default;
        }

        EnsureCapacity(count);
        for (var i = 0; i < count; i++) {
            _weights[i] = Math.Max(0, GetWeight(_visible[i]));
            _minWidths[i] = _visible[i].MinWidth;
        }

        var weights = _weights.AsSpan(0, count);
        var minWidths = _minWidths.AsSpan(0, count);
        var content = availableSize.Width - ColumnSpacing * (count - 1);

        // An unconstrained width can always host the row, so only a real shortfall stacks.
        _stacked = double.IsFinite(availableSize.Width)
                   && content < WeightedRowLayout.RequiredWidth(weights, minWidths);

        return _stacked
            ? MeasureStacked(availableSize, count)
            : MeasureRow(availableSize, content, count);
    }

    private Size MeasureRow(Size availableSize, double content, int count) {
        var widths = _widths.AsSpan(0, count);
        if (double.IsFinite(content))
            WeightedRowLayout.Split(Math.Max(0, content), _weights.AsSpan(0, count), widths);
        else
            widths.Fill(double.PositiveInfinity);

        var height = 0.0;
        var width = 0.0;
        for (var i = 0; i < count; i++) {
            _visible[i].Measure(new Size(widths[i], availableSize.Height));
            height = Math.Max(height, _visible[i].DesiredSize.Height);
            width += double.IsFinite(widths[i]) ? widths[i] : _visible[i].DesiredSize.Width;
        }

        return new Size(width + ColumnSpacing * (count - 1), height);
    }

    private Size MeasureStacked(Size availableSize, int count) {
        var height = 0.0;
        var width = 0.0;
        for (var i = 0; i < count; i++) {
            _visible[i].Measure(new Size(availableSize.Width, double.PositiveInfinity));
            height += _visible[i].DesiredSize.Height;
            width = Math.Max(width, _visible[i].DesiredSize.Width);
        }

        return new Size(double.IsFinite(availableSize.Width) ? availableSize.Width : width,
                        height + RowSpacing * (count - 1));
    }

    protected override Size ArrangeOverride(Size finalSize) {
        var count = _visible.Count;
        if (count == 0)
            return finalSize;

        if (_stacked) {
            var y = 0.0;
            for (var i = 0; i < count; i++) {
                var height = _visible[i].DesiredSize.Height;
                _visible[i].Arrange(new Rect(0, y, finalSize.Width, height));
                y += height + RowSpacing;
            }
            return finalSize;
        }

        // Re-split against the arranged width; the stacked/row decision itself stays from measure so
        // it cannot disagree with the heights measured under it.
        var widths = _widths.AsSpan(0, count);
        WeightedRowLayout.Split(Math.Max(0, finalSize.Width - ColumnSpacing * (count - 1)),
                                _weights.AsSpan(0, count), widths);

        var x = 0.0;
        for (var i = 0; i < count; i++) {
            _visible[i].Arrange(new Rect(x, 0, widths[i], finalSize.Height));
            x += widths[i] + ColumnSpacing;
        }

        return finalSize;
    }

    /// <summary>Collapsed children are skipped entirely, so a hidden panel neither takes a slice nor
    /// shifts the row's proportions.</summary>
    private void CollectVisible() {
        _visible.Clear();
        foreach (var child in Children)
            if (child.IsVisible)
                _visible.Add(child);
    }

    private void EnsureCapacity(int count) {
        if (_weights.Length >= count)
            return;
        _weights = new double[count];
        _minWidths = new double[count];
        _widths = new double[count];
    }
}
