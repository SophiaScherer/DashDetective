using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.VisualTree;
using DashDetective.Shared.Controls;
using System;
using System.Collections.Generic;
using System.Linq;

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
/// The drag itself is <see cref="ReorderDrag"/>'s; this supplies the layout it works over.
/// </summary>
public class WidgetBoard : Panel, IReorderablePanel {
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

    /// <summary>The widget ids in display order, two-way so a drag reports its result back to the page
    /// that persists it. Ids the board does not have are ignored; ones it has that are missing keep
    /// their declared position.</summary>
    public static readonly StyledProperty<IReadOnlyList<string>?> OrderProperty =
        AvaloniaProperty.Register<WidgetBoard, IReadOnlyList<string>?>(
            nameof(Order), defaultBindingMode: BindingMode.TwoWay);

    private readonly List<Control> _visible = new();
    private WidgetSlot[] _slots = Array.Empty<WidgetSlot>();
    private double[] _slotWidths = Array.Empty<double>();
    private List<int> _rowEnds = new();
    private double[] _rowHeights = Array.Empty<double>();
    private Rect2[] _slotRects = Array.Empty<Rect2>();

    private readonly ChildOrder _childOrder = new();
    private readonly ReorderDrag _drag;
    private bool _applyingOrder;

    static WidgetBoard() {
        AffectsParentMeasure<WidgetBoard>(
            WeightProperty, MaxSlotWidthProperty, BreakBeforeProperty, StretchProperty);
        AffectsMeasure<WidgetBoard>(ColumnSpacingProperty, RowSpacingProperty);
    }

    public WidgetBoard() => _drag = new ReorderDrag(this);

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

    public IReadOnlyList<string>? Order {
        get => GetValue(OrderProperty);
        set => SetValue(OrderProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change) {
        base.OnPropertyChanged(change);
        if (change.Property == OrderProperty && !_applyingOrder && ApplySavedOrder())
            InvalidateMeasure();
    }

    private bool ApplySavedOrder() => _childOrder.ApplySaved(Order, DeclaredIds());

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

    /// <summary>Raised with the new widget-id order once a drag commits.</summary>
    public event Action<IReadOnlyList<string>>? OrderChanged;

    // ----- Drag to reorder: the board's half of it. The pointer is ReorderDrag's. -----

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e) {
        base.OnAttachedToVisualTree(e);
        _drag.Attach();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e) {
        _drag.Detach();
        base.OnDetachedFromVisualTree(e);
    }

    Panel IReorderablePanel.Surface => this;

    IReadOnlyList<Control> IReorderablePanel.Items => _visible;

    /// <summary>A press may drag from a header only, and must miss every control in it — Storage keeps
    /// a drive picker up there, which a drag would otherwise swallow.</summary>
    public bool TryGetHandle(Visual source, out ReorderHandle handle) {
        handle = default;
        if (!HitHeader(source, out var panel, out var header))
            return false;

        var child = BoardChildOf(panel);
        if (child is null || !_visible.Contains(child))
            return false;

        handle = new ReorderHandle(child, panel, header);
        return true;
    }

    public int DropIndexAt(Point pointer) =>
        WidgetBoardLayout.DropIndex(
            _slotRects.AsSpan(0, _visible.Count),
            System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_rowEnds),
            pointer.X, pointer.Y);

    public void BeginPreview() => _childOrder.BeginPreview();

    public bool PreviewMove(Control item, int target) =>
        _childOrder.Move(Children.IndexOf(item), target, IsChildVisible);

    public void CommitPreview() {
        var ids = _childOrder.Commit(DeclaredIds());
        if (ids.Count == 0)
            return;

        _applyingOrder = true;
        Order = ids;
        _applyingOrder = false;
        OrderChanged?.Invoke(ids);
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

                // The dragged widget keeps the width it was picked up at, so its content cannot
                // re-wrap under the cursor while the rows behind it re-pack.
                var measureWidth = ReferenceEquals(child, _drag.Dragged)
                    ? _drag.DragSize.Width
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
                var index = start + i;
                var box = new Rect(x, y, rowWidths[i], _rowHeights[r]);
                _slotRects[index] = new Rect2(box.X, box.Y, box.Width, box.Height);

                // Follows the pointer, keeping its place in Children so nothing is reparented and no
                // binding is torn down. Anchored to the pointer rather than offset from its slot: the
                // previewed slot jumps a whole column the moment a reorder commits, which threw the
                // widget out from under the cursor. The slot keeps its real size, and is what the drop
                // hint outlines.
                if (ReferenceEquals(_visible[index], _drag.Dragged)) {
                    _drag.ShowHint(box);
                    box = _drag.DragBox();
                }

                _visible[index].Arrange(box);
                x += rowWidths[i] + ColumnSpacing;
            }

            y += _rowHeights[r] + RowSpacing;
            start = end;
        }

        return finalSize;
    }

    /// <summary>Collapsed children are skipped, so a hidden widget takes no slot.</summary>
    private void CollectVisible() {
        if (_childOrder.Sync(Children.Count))
            ApplySavedOrder();

        _visible.Clear();
        foreach (var index in _childOrder.Shown(_drag.IsDragging))
            if (index < Children.Count && Children[index].IsVisible)
                _visible.Add(Children[index]);
    }

    private bool IsChildVisible(int index) => index < Children.Count && Children[index].IsVisible;

    private List<string> DeclaredIds() {
        var ids = new List<string>(Children.Count);
        foreach (var child in Children)
            ids.Add(Reorder.IdOf(child));
        return ids;
    }

    private void BuildSlots() {
        if (_slots.Length < _visible.Count) {
            _slots = new WidgetSlot[_visible.Count];
            _slotWidths = new double[_visible.Count];
            _slotRects = new Rect2[_visible.Count];
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
