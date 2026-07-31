using Avalonia;
using Avalonia.Controls;
using System;
using System.Collections.Generic;

namespace DashDetective.Shared.Layout;

/// <summary>
/// Lays children out in equal-width columns that wrap to a new row rather than shrinking past
/// <see cref="MinItemWidth"/>. Replaces a <c>UniformGrid</c> with a hardcoded <c>Columns</c>, which
/// squeezes its cells instead of reflowing. Column count comes from <see cref="FlowLayout"/>.
///
/// The panel owns the gutter via <see cref="ColumnSpacing"/> / <see cref="RowSpacing"/>, so call
/// sites drop the negative-margin-on-panel idiom — that cancellation assumes a fixed column count
/// and stops working once the count varies.
/// </summary>
public class UniformFlowPanel : Panel {
    /// <summary>Narrowest a column may get before the panel wraps to another row. 0 disables wrapping.</summary>
    public static readonly StyledProperty<double> MinItemWidthProperty =
        AvaloniaProperty.Register<UniformFlowPanel, double>(nameof(MinItemWidth));

    /// <summary>Upper bound on columns however wide the panel gets. 0 (the default) means unlimited.</summary>
    public static readonly StyledProperty<int> MaxColumnsProperty =
        AvaloniaProperty.Register<UniformFlowPanel, int>(nameof(MaxColumns));

    public static readonly StyledProperty<double> ColumnSpacingProperty =
        AvaloniaProperty.Register<UniformFlowPanel, double>(nameof(ColumnSpacing));

    public static readonly StyledProperty<double> RowSpacingProperty =
        AvaloniaProperty.Register<UniformFlowPanel, double>(nameof(RowSpacing));

    private readonly List<Control> _visible = new();
    private readonly List<double> _rowHeights = new();
    private int _columns = 1;

    static UniformFlowPanel() {
        AffectsMeasure<UniformFlowPanel>(MinItemWidthProperty, MaxColumnsProperty,
                                         ColumnSpacingProperty, RowSpacingProperty);
    }

    public double MinItemWidth {
        get => GetValue(MinItemWidthProperty);
        set => SetValue(MinItemWidthProperty, value);
    }

    public int MaxColumns {
        get => GetValue(MaxColumnsProperty);
        set => SetValue(MaxColumnsProperty, value);
    }

    /// <summary>Horizontal gap between columns.</summary>
    public double ColumnSpacing {
        get => GetValue(ColumnSpacingProperty);
        set => SetValue(ColumnSpacingProperty, value);
    }

    /// <summary>Vertical gap between rows.</summary>
    public double RowSpacing {
        get => GetValue(RowSpacingProperty);
        set => SetValue(RowSpacingProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize) {
        CollectVisible();
        _rowHeights.Clear();
        if (_visible.Count == 0) {
            _columns = 1;
            return default;
        }

        _columns = FlowLayout.ColumnCount(availableSize.Width, MinItemWidth, ColumnSpacing,
                                          _visible.Count, MaxColumns);
        var itemWidth = FlowLayout.ItemWidth(availableSize.Width, _columns, ColumnSpacing);

        // An unconstrained slot (Auto column, horizontal StackPanel) has no width to divide, so let
        // the children ask for what they want and size the columns to the widest.
        var unconstrained = !double.IsFinite(availableSize.Width);
        var measureWidth = unconstrained ? double.PositiveInfinity : itemWidth;

        var rowHeight = 0.0;
        var widest = 0.0;
        for (var i = 0; i < _visible.Count; i++) {
            var child = _visible[i];
            child.Measure(new Size(measureWidth, double.PositiveInfinity));
            rowHeight = Math.Max(rowHeight, child.DesiredSize.Height);
            widest = Math.Max(widest, child.DesiredSize.Width);
            if ((i + 1) % _columns == 0) {
                _rowHeights.Add(rowHeight);
                rowHeight = 0;
            }
        }
        if (_visible.Count % _columns != 0)
            _rowHeights.Add(rowHeight);

        if (unconstrained)
            itemWidth = widest;

        var height = 0.0;
        foreach (var h in _rowHeights)
            height += h;
        height += RowSpacing * Math.Max(0, _rowHeights.Count - 1);

        var width = itemWidth * _columns + ColumnSpacing * (_columns - 1);
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize) {
        if (_visible.Count == 0 || _rowHeights.Count == 0)
            return finalSize;

        // Column count is kept from the measure pass so it stays in step with the row heights
        // measured against it; only the width is re-derived, since arrange can differ slightly.
        var itemWidth = FlowLayout.ItemWidth(finalSize.Width, _columns, ColumnSpacing);
        var y = 0.0;

        for (var row = 0; row < _rowHeights.Count; row++) {
            var rowHeight = _rowHeights[row];
            for (var column = 0; column < _columns; column++) {
                var index = row * _columns + column;
                if (index >= _visible.Count)
                    break;
                var x = column * (itemWidth + ColumnSpacing);
                _visible[index].Arrange(new Rect(x, y, itemWidth, rowHeight));
            }
            y += rowHeight + RowSpacing;
        }

        return finalSize;
    }

    /// <summary>Collapsed children are skipped entirely so one never occupies a column.</summary>
    private void CollectVisible() {
        _visible.Clear();
        foreach (var child in Children)
            if (child.IsVisible)
                _visible.Add(child);
    }
}
