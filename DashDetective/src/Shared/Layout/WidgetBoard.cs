using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using DashDetective.Shared;
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
/// </summary>
public class WidgetBoard : Panel {
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

    // _order is the board's ordering of Children, _preview the order being tried mid-drag. A drag
    // never mutates Children: that is re-entrant mid-layout and would detach a live Sparkline.
    private readonly List<int> _order = new();
    private readonly List<int> _preview = new();
    private bool _dragPending;
    private bool _dragging;
    private Point _pressPoint;
    private Point _pointer;
    private Control? _dragged;
    private WidgetPanel? _draggedPanel;
    private bool _applyingOrder;

    // Where in the widget it was picked up, as fractions of its size, and the size it had then. Both
    // are what keep the grip under the cursor when the slots beneath it re-pack.
    private double _grabX;
    private double _grabY;
    private Size _dragSize;
    private DragDropHint? _hint;
    private Panel? _handleUnderPointer;

    private static readonly Cursor DragCursor = new(StandardCursorType.Hand);

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

    public IReadOnlyList<string>? Order {
        get => GetValue(OrderProperty);
        set => SetValue(OrderProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change) {
        base.OnPropertyChanged(change);
        if (change.Property == OrderProperty && !_applyingOrder)
            ApplySavedOrder(Order);
    }

    /// <summary>Reorders the board to a saved order, keeping any widget the save does not name at its
    /// declared position.</summary>
    private void ApplySavedOrder(IReadOnlyList<string>? saved) {
        if (saved is null || saved.Count == 0 || Children.Count == 0)
            return;

        var declared = new List<string>(Children.Count);
        foreach (var child in Children)
            declared.Add(WidgetIdOf(child));

        var resolved = WidgetOrders.Resolve(declared.FindAll(id => id.Length > 0), saved);
        var position = new Dictionary<string, int>();
        for (var i = 0; i < resolved.Count; i++)
            position[resolved[i]] = i;

        // A child with no id (a card strip) keeps its declared index, so it cannot be dragged past.
        EnsureOrder();
        _order.Sort((a, b) => Key(a).CompareTo(Key(b)));
        InvalidateMeasure();

        double Key(int index) =>
            declared[index].Length > 0 && position.TryGetValue(declared[index], out var rank)
                ? rank
                : index;
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

    /// <summary>Raised with the new widget-id order once a drag commits.</summary>
    public event Action<IReadOnlyList<string>>? OrderChanged;

    // ----- Drag to reorder -----

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e) {
        base.OnAttachedToVisualTree(e);
        AddHandler(PointerPressedEvent, OnPreviewPressed, RoutingStrategies.Tunnel);
        AddHandler(PointerMovedEvent, OnPointerMove, RoutingStrategies.Tunnel);
        AddHandler(PointerReleasedEvent, OnPointerUp, RoutingStrategies.Tunnel);
        AddHandler(PointerCaptureLostEvent, OnCaptureLost, RoutingStrategies.Tunnel);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e) {
        RemoveHandler(PointerPressedEvent, OnPreviewPressed);
        RemoveHandler(PointerMovedEvent, OnPointerMove);
        RemoveHandler(PointerReleasedEvent, OnPointerUp);
        RemoveHandler(PointerCaptureLostEvent, OnCaptureLost);
        base.OnDetachedFromVisualTree(e);
    }

    private void OnPreviewPressed(object? sender, PointerPressedEventArgs e) {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;
        if (e.Source is not Visual source || !HitHeader(source, out var panel, out _))
            return;

        var child = BoardChildOf(panel);
        if (child is null || !_visible.Contains(child))
            return;

        _dragged = child;
        _draggedPanel = panel;
        _pressPoint = e.GetPosition(this);
        _dragSize = child.Bounds.Size;
        _grabX = Fraction(_pressPoint.X - child.Bounds.X, _dragSize.Width);
        _grabY = Fraction(_pressPoint.Y - child.Bounds.Y, _dragSize.Height);
        _dragPending = true;
        e.Pointer.Capture(this);
    }

    // A fraction, not a pixel offset: a widget grabbed near its right edge would otherwise end up
    // beside the cursor rather than under it once its slot changed size.
    private static double Fraction(double offset, double extent) =>
        extent > 0 ? Math.Clamp(offset / extent, 0, 1) : 0;

    private void OnPointerMove(object? sender, PointerEventArgs e) {
        if (!_dragPending || _dragged is null) {
            ShowHandleCursor(e.Source as Visual);
            return;
        }

        _pointer = e.GetPosition(this);
        if (!_dragging) {
            var delta = _pointer - _pressPoint;
            if (Math.Abs(delta.X) < PointerDrag.Threshold && Math.Abs(delta.Y) < PointerDrag.Threshold)
                return;
            _dragging = true;
            _preview.Clear();
            _preview.AddRange(_order);
            _draggedPanel?.Classes.Add("dragging");
            _dragged.ZIndex = 10;
            _hint = new DragDropHint(this, this.Radius("RadiusPanel", 4));
            _hint.Attach();
        }

        // Re-pack under the order being tried, so the others shift as the drag moves.
        if (MovePreviewTo(DropTarget()))
            InvalidateMeasure();
        InvalidateArrange();
    }

    protected override void OnPointerExited(PointerEventArgs e) {
        base.OnPointerExited(e);
        if (!_dragPending)
            ShowHandleCursor(null);
    }

    /// <summary>Points the cursor at the drag handle under it, from the same predicate that starts a
    /// drag. Driving both from one place is what stops a header advertising a drag it cannot do: a
    /// panel outside a board, or a control sitting inside a header, offers none.</summary>
    private void ShowHandleCursor(Visual? source) {
        Panel? handle = null;
        if (source is not null && HitHeader(source, out var panel, out var header)
            && BoardChildOf(panel) is { } child && _visible.Contains(child))
            handle = header;

        if (ReferenceEquals(handle, _handleUnderPointer))
            return;

        if (_handleUnderPointer is not null)
            _handleUnderPointer.Cursor = null;
        if (handle is not null)
            handle.Cursor = DragCursor;
        _handleUnderPointer = handle;
    }

    private void OnPointerUp(object? sender, PointerReleasedEventArgs e) {
        // Only a press this board claimed may release the capture. Releasing unconditionally strips the
        // capture a button took on its own press, and it then never sees the release that clicks it.
        if (!_dragPending)
            return;

        if (_dragging) {
            _order.Clear();
            _order.AddRange(_preview);
            var ids = _visible.Select(WidgetIdOf).Where(id => id.Length > 0).ToList();
            if (ids.Count > 0) {
                _applyingOrder = true;
                Order = ids;
                _applyingOrder = false;
                OrderChanged?.Invoke(ids);
            }
        }

        e.Pointer.Capture(null);
        EndDrag();
    }

    /// <summary>Where the pointer says the dragged widget belongs, in the preview order.</summary>
    private int DropTarget() {
        var drop = WidgetBoardLayout.DropIndex(
            _slotRects.AsSpan(0, _visible.Count),
            System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_rowEnds),
            _pointer.X, _pointer.Y);

        var current = _dragged is null ? -1 : _visible.IndexOf(_dragged);
        return drop > current ? drop - 1 : drop;
    }

    /// <summary>Moves the dragged widget in the preview order; false when it is already there, so a
    /// wobble does not re-pack every frame.</summary>
    private bool MovePreviewTo(int target) {
        if (_dragged is null)
            return false;

        var child = Children.IndexOf(_dragged);
        var from = _preview.IndexOf(child);
        if (from < 0)
            return false;

        target = Math.Clamp(target, 0, _preview.Count - 1);
        if (target == from)
            return false;

        _preview.RemoveAt(from);
        _preview.Insert(target, child);
        return true;
    }

    private void OnCaptureLost(object? sender, PointerCaptureLostEventArgs e) {
        if (_dragPending)
            EndDrag();
    }

    /// <summary>The press must hit a header and miss every control in it — Storage keeps a drive
    /// picker up there, which a drag would otherwise swallow.</summary>
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

    // Reached from release and from lost capture alike, so the lifted state can never stick.
    private void EndDrag() {
        _hint?.Hide();
        _hint = null;
        _draggedPanel?.Classes.Remove("dragging");
        if (_dragged is not null)
            _dragged.ZIndex = 0;
        _draggedPanel = null;
        _dragged = null;
        _dragging = false;
        _dragPending = false;
        InvalidateMeasure();
        InvalidateArrange();
    }

    private void EnsureOrder() {
        if (_order.Count == Children.Count)
            return;
        _order.Clear();
        for (var i = 0; i < Children.Count; i++)
            _order.Add(i);
    }

    /// <summary>The board's own child containing this control — not always the control itself, since
    /// a ConsolePanel wraps the WidgetPanel whose header was pressed.</summary>
    private Control? BoardChildOf(Control control) {
        for (Visual? node = control; node is not null; node = node.GetVisualParent())
            if (node is Control candidate && ReferenceEquals(candidate.GetVisualParent(), this))
                return candidate;
        return null;
    }

    private static string WidgetIdOf(Control control) =>
        control is IWidgetIdentity identity ? identity.WidgetId ?? "" : "";

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
                var measureWidth = _dragging && ReferenceEquals(child, _dragged)
                    ? _dragSize.Width
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

                // Follows the pointer, keeping its place in Children so nothing is reparented and
                // no binding is torn down. Anchored to the pointer rather than offset from its slot:
                // the previewed slot jumps a whole column the moment a reorder commits, which threw
                // the widget out from under the cursor. The slot keeps its real size, and is what the
                // drop hint outlines.
                if (_dragging && ReferenceEquals(_visible[index], _dragged)) {
                    _hint?.ShowFrom(this, box);
                    var drag = WidgetBoardLayout.DragRect(_dragSize.Width, _dragSize.Height,
                                                          _pointer.X, _pointer.Y, _grabX, _grabY);
                    box = new Rect(drag.Left, drag.Top, drag.Width, drag.Height);
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
        EnsureOrder();
        _visible.Clear();
        foreach (var index in _dragging ? _preview : _order)
            if (index < Children.Count && Children[index].IsVisible)
                _visible.Add(Children[index]);
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
