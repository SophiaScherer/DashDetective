using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;
using DashDetective.Shared.Controls;
using System;
using System.Collections.Generic;

namespace DashDetective.Shared.Layout;

/// <summary>
/// A page's widgets as one flow: packed into rows that fit the window, capped in width, and
/// draggable by their headers. Replaces the fixed rows each page used to author, whose frozen
/// membership is why a wide window stretched every panel instead of gaining a column.
///
/// The cap is attached here rather than being the child's own <c>MaxWidth</c>: Avalonia's arrange
/// clamps a stretched child to MaxWidth and then centres it, leaving a dead margin down each side.
/// <c>MinWidth</c> stays the child's own, so there is one source of truth.
///
/// The drag itself is <see cref="ReorderablePanel"/>'s; this supplies the layout it works over.
/// </summary>
public class WidgetBoard : ReorderablePanel {
    /// <summary>This child's share of its row. Row-local, so a drag changes what it buys.</summary>
    public static readonly AttachedProperty<double> WeightProperty =
        AvaloniaProperty.RegisterAttached<WidgetBoard, Control, double>("Weight", 1.0);

    /// <summary>Width past which this widget stops being readable. Unset means no cap.</summary>
    public static readonly AttachedProperty<double> MaxSlotWidthProperty =
        AvaloniaProperty.RegisterAttached<WidgetBoard, Control, double>(
            "MaxSlotWidth", double.PositiveInfinity);

    /// <summary>Start a new row at this child even where the previous one had room.</summary>
    public static readonly AttachedProperty<bool> BreakBeforeProperty =
        AvaloniaProperty.RegisterAttached<WidgetBoard, Control, bool>("BreakBefore");

    /// <summary>This child owns its row, which is also what pins a non-widget child in place.</summary>
    public static readonly AttachedProperty<bool> StretchProperty =
        AvaloniaProperty.RegisterAttached<WidgetBoard, Control, bool>("Stretch");

    public static readonly StyledProperty<double> ColumnSpacingProperty =
        AvaloniaProperty.Register<WidgetBoard, double>(nameof(ColumnSpacing));

    public static readonly StyledProperty<double> RowSpacingProperty =
        AvaloniaProperty.Register<WidgetBoard, double>(nameof(RowSpacing));

    private WidgetSlot[] _slots = Array.Empty<WidgetSlot>();
    private double[] _slotWidths = Array.Empty<double>();
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

    /// <summary>A press may drag from a header only, and must miss every control in it — Storage keeps
    /// a drive picker up there, which a drag would otherwise swallow.</summary>
    public override bool TryGetHandle(Visual source, out ReorderHandle handle) {
        handle = default;
        if (!HitHeader(source, out var panel, out var header))
            return false;

        var child = BoardChildOf(panel);
        if (child is null || !IsShown(child))
            return false;

        handle = new ReorderHandle(child, panel, header);
        return true;
    }

    private static bool HitHeader(Visual source, out WidgetPanel panel, out Panel header) {
        panel = null!;
        header = null!;
        foreach (var node in source.GetSelfAndVisualAncestors()) {
            switch (node) {
                case Button or ToggleButton or TextBox or ComboBox or ScrollBar:
                    return false;
                case Panel { Name: "PART_Header" } hit:
                    header = hit;
                    break;
                case WidgetPanel found:
                    panel = found;
                    return header is not null;
            }
        }
        return false;
    }

    /// <summary>The board's own child containing this control — not always the control itself, since
    /// a ConsolePanel wraps the WidgetPanel whose header was pressed.</summary>
    private Control? BoardChildOf(Control control) {
        for (Visual? node = control; node is not null; node = node.GetVisualParent())
            if (node is Control candidate && ReferenceEquals(candidate.GetVisualParent(), this))
                return candidate;
        return null;
    }

    protected override Size MeasureOverride(Size availableSize) {
        CollectVisible();
        RowEnds.Clear();
        if (Visible.Count == 0)
            return default;

        BuildSlots();
        RowEnds.AddRange(WidgetBoardLayout.PackRows(_slots.AsSpan(0, Visible.Count),
                                                    availableSize.Width, ColumnSpacing));
        EnsureRowCapacity(RowEnds.Count);

        var totalHeight = 0.0;
        var totalWidth = 0.0;
        var start = 0;

        for (var r = 0; r < RowEnds.Count; r++) {
            var end = RowEnds[r];
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
                var child = Visible[start + i];

                // The dragged widget keeps the width it was picked up at, so its content cannot
                // re-wrap under the cursor while the rows behind it re-pack.
                var measureWidth = ReferenceEquals(child, Drag.Dragged)
                    ? Drag.DragSize.Width
                    : rowWidths[i];
                child.Measure(new Size(measureWidth, double.PositiveInfinity));
                rowHeight = Math.Max(rowHeight, child.DesiredSize.Height);
                rowWidth += double.IsFinite(rowWidths[i]) ? rowWidths[i] : child.DesiredSize.Width;
            }

            _rowHeights[r] = rowHeight;
            totalHeight += rowHeight;
            totalWidth = Math.Max(totalWidth, rowWidth + ColumnSpacing * (count - 1));
            start = end;
        }

        totalHeight += RowSpacing * (RowEnds.Count - 1);
        return new Size(double.IsFinite(availableSize.Width) ? availableSize.Width : totalWidth, totalHeight);
    }

    protected override Size ArrangeOverride(Size finalSize) {
        if (Visible.Count == 0 || RowEnds.Count == 0)
            return finalSize;

        // Re-split against the arranged width; the row MEMBERSHIP stays as measured, so the heights
        // measured under it cannot disagree with what is drawn.
        var y = 0.0;
        var start = 0;

        for (var r = 0; r < RowEnds.Count; r++) {
            var end = RowEnds[r];
            var count = end - start;
            var row = _slots.AsSpan(start, count);
            var rowWidths = _slotWidths.AsSpan(start, count);
            WidgetBoardLayout.SplitRow(row, finalSize.Width, ColumnSpacing, rowWidths);

            var x = 0.0;
            for (var i = 0; i < count; i++) {
                // A dragged widget keeps its place in Children so nothing is reparented and no
                // binding is torn down; only the box it is arranged at follows the pointer.
                var index = start + i;
                Visible[index].Arrange(Placed(index, new Rect(x, y, rowWidths[i], _rowHeights[r])));
                x += rowWidths[i] + ColumnSpacing;
            }

            y += _rowHeights[r] + RowSpacing;
            start = end;
        }

        return finalSize;
    }

    private void BuildSlots() {
        if (_slots.Length < Visible.Count) {
            _slots = new WidgetSlot[Visible.Count];
            _slotWidths = new double[Visible.Count];
        }

        for (var i = 0; i < Visible.Count; i++) {
            var child = Visible[i];
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
