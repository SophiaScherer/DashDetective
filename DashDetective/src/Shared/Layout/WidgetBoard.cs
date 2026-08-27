using Avalonia;
using Avalonia.Controls;
using System;
using System.Collections.Generic;

namespace DashDetective.Shared.Layout;

/// <summary>
/// A page's widgets as one flow: packs them into rows that fit the window, caps how wide any one of
/// them gets, and spends the width left over on another column rather than on a wider widget.
///
/// Replaces the <c>StackPanel</c> of two or three <see cref="WeightedRowPanel"/>s each page used to
/// author. Row membership was frozen in that markup, which is what made an ultrawide simply stretch
/// every panel — and what would make dragging a widget from one row to another impossible. The board
/// owns the whole page, so both fall out of one arithmetic (<see cref="WidgetBoardLayout"/>).
///
/// <see cref="MaxSlotWidthProperty"/> is attached here rather than being each child's own
/// <c>MaxWidth</c> on purpose: Avalonia's arrange clamps a stretched child to its MaxWidth and then
/// *centres* it in the slot, which would leave the dead margin down each side that the cap exists to
/// avoid. Making the board the only clamper keeps alignment out of it. <c>MinWidth</c> stays the
/// child's own, for the reason <see cref="WeightedRowPanel"/> already gives: one source of truth, and
/// Avalonia keeps honouring it inside the child's own measure.
/// </summary>
public class WidgetBoard : Panel {
    /// <summary>This child's share of its row. Row-local — it means "this much of whatever shares my
    /// row", so a drag that changes the row's membership changes what it buys. Keep them near 1.</summary>
    public static readonly AttachedProperty<double> WeightProperty =
        AvaloniaProperty.RegisterAttached<WidgetBoard, Control, double>("Weight", 1.0);

    /// <summary>The width past which this widget stops being readable. Unset means no cap, which also
    /// means the row it sits in can never be judged too roomy.</summary>
    public static readonly AttachedProperty<double> MaxSlotWidthProperty =
        AvaloniaProperty.RegisterAttached<WidgetBoard, Control, double>(
            "MaxSlotWidth", double.PositiveInfinity);

    /// <summary>Start a new row at this child even where the previous one had room.</summary>
    public static readonly AttachedProperty<bool> BreakBeforeProperty =
        AvaloniaProperty.RegisterAttached<WidgetBoard, Control, bool>("BreakBefore");

    /// <summary>This child owns its row: a card strip or a wide table meant to span the page. Also
    /// what pins a non-widget child in place, since nothing can be packed beside it.</summary>
    public static readonly AttachedProperty<bool> StretchProperty =
        AvaloniaProperty.RegisterAttached<WidgetBoard, Control, bool>("Stretch");

    public static readonly StyledProperty<double> ColumnSpacingProperty =
        AvaloniaProperty.Register<WidgetBoard, double>(nameof(ColumnSpacing));

    public static readonly StyledProperty<double> RowSpacingProperty =
        AvaloniaProperty.Register<WidgetBoard, double>(nameof(RowSpacing));

    private readonly List<Control> _visible = new();
    private WidgetSlot[] _slots = Array.Empty<WidgetSlot>();
    private double[] _slotWidths = Array.Empty<double>();
    private List<int> _rowEnds = new();
    private double[] _rowHeights = Array.Empty<double>();

    static WidgetBoard() {
        AffectsParentMeasure<WidgetBoard>(
            WeightProperty, MaxSlotWidthProperty, BreakBeforeProperty, StretchProperty);
        AffectsMeasure<WidgetBoard>(ColumnSpacingProperty, RowSpacingProperty);
    }

    /// <summary>Horizontal gap between widgets sharing a row.</summary>
    public double ColumnSpacing {
        get => GetValue(ColumnSpacingProperty);
        set => SetValue(ColumnSpacingProperty, value);
    }

    /// <summary>Vertical gap between rows.</summary>
    public double RowSpacing {
        get => GetValue(RowSpacingProperty);
        set => SetValue(RowSpacingProperty, value);
    }

    public static double GetWeight(Control control) => control.GetValue(WeightProperty);

    public static void SetWeight(Control control, double value) => control.SetValue(WeightProperty, value);

    public static double GetMaxSlotWidth(Control control) => control.GetValue(MaxSlotWidthProperty);

    public static void SetMaxSlotWidth(Control control, double value) =>
        control.SetValue(MaxSlotWidthProperty, value);

    public static bool GetBreakBefore(Control control) => control.GetValue(BreakBeforeProperty);

    public static void SetBreakBefore(Control control, bool value) =>
        control.SetValue(BreakBeforeProperty, value);

    public static bool GetStretch(Control control) => control.GetValue(StretchProperty);

    public static void SetStretch(Control control, bool value) => control.SetValue(StretchProperty, value);

    protected override Size MeasureOverride(Size availableSize) {
        CollectVisible();
        if (_visible.Count == 0) {
            _rowEnds = new List<int>();
            return default;
        }

        BuildSlots();
        _rowEnds = WidgetBoardLayout.PackRows(_slots.AsSpan(0, _visible.Count),
                                              availableSize.Width, ColumnSpacing);
        EnsureRowCapacity(_rowEnds.Count);

        var totalHeight = 0.0;
        var totalWidth = 0.0;
        var start = 0;

        for (var r = 0; r < _rowEnds.Count; r++) {
            var end = _rowEnds[r];
            var count = end - start;
            var row = _slots.AsSpan(start, count);
            var rowWidths = _slotWidths.AsSpan(start, count);

            // An unconstrained width has no share to divide, so each child measures to its own content.
            if (double.IsFinite(availableSize.Width))
                WidgetBoardLayout.SplitRow(row, availableSize.Width, ColumnSpacing, rowWidths);
            else
                rowWidths.Fill(double.PositiveInfinity);

            var rowHeight = 0.0;
            var rowWidth = 0.0;
            for (var i = 0; i < count; i++) {
                var child = _visible[start + i];
                child.Measure(new Size(rowWidths[i], double.PositiveInfinity));
                rowHeight = Math.Max(rowHeight, child.DesiredSize.Height);
                rowWidth += double.IsFinite(rowWidths[i]) ? rowWidths[i] : child.DesiredSize.Width;
            }

            _rowHeights[r] = rowHeight;
            totalHeight += rowHeight;
            totalWidth = Math.Max(totalWidth, rowWidth + ColumnSpacing * (count - 1));
            start = end;
        }

        totalHeight += RowSpacing * (_rowEnds.Count - 1);
        return new Size(double.IsFinite(availableSize.Width) ? availableSize.Width : totalWidth, totalHeight);
    }

    protected override Size ArrangeOverride(Size finalSize) {
        if (_visible.Count == 0 || _rowEnds.Count == 0)
            return finalSize;

        // Re-split against the arranged width; the row MEMBERSHIP stays as measured, so the heights
        // measured under it cannot disagree with what is drawn.
        var y = 0.0;
        var start = 0;

        for (var r = 0; r < _rowEnds.Count; r++) {
            var end = _rowEnds[r];
            var count = end - start;
            var row = _slots.AsSpan(start, count);
            var rowWidths = _slotWidths.AsSpan(start, count);
            WidgetBoardLayout.SplitRow(row, finalSize.Width, ColumnSpacing, rowWidths);

            var x = 0.0;
            for (var i = 0; i < count; i++) {
                _visible[start + i].Arrange(new Rect(x, y, rowWidths[i], _rowHeights[r]));
                x += rowWidths[i] + ColumnSpacing;
            }

            y += _rowHeights[r] + RowSpacing;
            start = end;
        }

        return finalSize;
    }

    /// <summary>Collapsed children are skipped entirely, so a hidden widget neither takes a slot nor
    /// shifts the row's proportions.</summary>
    private void CollectVisible() {
        _visible.Clear();
        foreach (var child in Children)
            if (child.IsVisible)
                _visible.Add(child);
    }

    private void BuildSlots() {
        if (_slots.Length < _visible.Count) {
            _slots = new WidgetSlot[_visible.Count];
            _slotWidths = new double[_visible.Count];
        }

        for (var i = 0; i < _visible.Count; i++) {
            var child = _visible[i];
            _slots[i] = new WidgetSlot(
                Math.Max(0, GetWeight(child)),
                child.MinWidth,
                GetMaxSlotWidth(child),
                GetBreakBefore(child),
                GetStretch(child));
        }
    }

    private void EnsureRowCapacity(int rows) {
        if (_rowHeights.Length < rows)
            _rowHeights = new double[rows];
    }
}
