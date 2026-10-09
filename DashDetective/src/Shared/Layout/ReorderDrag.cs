using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DashDetective.Shared;
using System;

namespace DashDetective.Shared.Layout;

/// <summary>
/// Drag-to-reorder, for any panel that can say where its children are: the pointer handling, the
/// movement threshold, the drag cursor, the previewed order and the drop hint. The panel it drives
/// supplies the layout through <see cref="IReorderablePanel"/>.
///
/// It lives apart from the panel because there is more than one — a page's widget board and a strip
/// of cards inside one reorder the same way and must feel the same doing it.
/// </summary>
public sealed class ReorderDrag {
    private static readonly Cursor GripCursor = new(StandardCursorType.Hand);

    private readonly IReorderablePanel _host;
    private readonly DropTarget _target = new();

    private bool _pending;              // pointer is down on a handle, not yet past the threshold
    private bool _dragging;             // past it: previewing a reorder
    private Point _press;               // press point, in the panel's coordinates
    private Point _pointer;             // where the pointer is now
    private double _grabX;              // where in the item it was picked up, as fractions of its
    private double _grabY;              // size — see Fraction
    private Size _size;                 // the size it had then, frozen for the drag
    private Control? _item;
    private Control? _lifted;
    private DragDropHint? _hint;
    private InputElement? _gripUnderPointer;

    public ReorderDrag(IReorderablePanel host) => _host = host;

    /// <summary>Whether a drag is in progress, which is what tells the panel to lay out the previewed
    /// order rather than the settled one.</summary>
    public bool IsDragging => _dragging;

    /// <summary>The item being dragged, or null.</summary>
    public Control? Dragged => _dragging ? _item : null;

    /// <summary>The size the dragged item is held at. It keeps the size it was picked up at, so its
    /// content cannot re-wrap under the cursor while the panel behind it re-packs.</summary>
    public Size DragSize => _size;

    public void Attach() {
        _host.Surface.AddHandler(InputElement.PointerPressedEvent, OnPressed, RoutingStrategies.Tunnel);
        _host.Surface.AddHandler(InputElement.PointerMovedEvent, OnMoved, RoutingStrategies.Tunnel);
        _host.Surface.AddHandler(InputElement.PointerReleasedEvent, OnReleased, RoutingStrategies.Tunnel);
        _host.Surface.AddHandler(InputElement.PointerCaptureLostEvent, OnCaptureLost, RoutingStrategies.Tunnel);

        // Exited is a direct event, so this fires for the panel itself rather than for every child the
        // pointer crosses on the way out.
        _host.Surface.AddHandler(InputElement.PointerExitedEvent, OnExited, RoutingStrategies.Direct);
    }

    public void Detach() {
        _host.Surface.RemoveHandler(InputElement.PointerPressedEvent, OnPressed);
        _host.Surface.RemoveHandler(InputElement.PointerMovedEvent, OnMoved);
        _host.Surface.RemoveHandler(InputElement.PointerReleasedEvent, OnReleased);
        _host.Surface.RemoveHandler(InputElement.PointerCaptureLostEvent, OnCaptureLost);
        _host.Surface.RemoveHandler(InputElement.PointerExitedEvent, OnExited);
    }

    /// <summary>Where the dragged item is drawn: under the pointer at the grip it was picked up by,
    /// at the size it had then.</summary>
    public Rect DragBox() {
        var box = WidgetBoardLayout.DragRect(_size.Width, _size.Height, _pointer.X, _pointer.Y,
                                             _grabX, _grabY);
        return new Rect(box.Left, box.Top, box.Width, box.Height);
    }

    /// <summary>Outlines the slot the dragged item will land in, given in the panel's coordinates.</summary>
    public void ShowHint(Rect slot) => _hint?.ShowFrom(_host.Surface, slot);

    private void OnPressed(object? sender, PointerPressedEventArgs e) {
        if (!e.GetCurrentPoint(_host.Surface).Properties.IsLeftButtonPressed)
            return;
        if (e.Source is not Visual source || !_host.TryGetHandle(source, out var handle))
            return;

        Press(handle, e.GetPosition(_host.Surface));
    }

    /// <summary>Picks the item up at this point, pending the movement threshold. Internal so tests can
    /// drive a drag without a pointer device.</summary>
    internal void Press(ReorderHandle handle, Point press) {
        (_item, _lifted, _) = handle;
        _press = press;
        _size = handle.Item.Bounds.Size;
        _grabX = Fraction(_press.X - handle.Item.Bounds.X, _size.Width);
        _grabY = Fraction(_press.Y - handle.Item.Bounds.Y, _size.Height);
        _pending = true;
    }

    // A fraction, not a pixel offset: an item grabbed near its right edge would otherwise end up
    // beside the cursor rather than under it once its slot changed size.
    private static double Fraction(double offset, double extent) =>
        extent > 0 ? Math.Clamp(offset / extent, 0, 1) : 0;

    private void OnMoved(object? sender, PointerEventArgs e) {
        if (!_pending || _item is null) {
            ShowGripCursor(e.Source as Visual);
            return;
        }

        MoveTo(e.GetPosition(_host.Surface), e.Pointer);
    }

    /// <summary>Follows the pointer here: starts the drag once past the threshold, then previews the
    /// slot it covers. <paramref name="device"/> is null only from tests, which have nothing to capture.</summary>
    internal void MoveTo(Point pointer, IPointer? device) {
        if (!_pending || _item is null)
            return;

        _pointer = pointer;
        if (!_dragging) {
            var delta = _pointer - _press;
            if (Math.Abs(delta.X) < PointerDrag.Threshold && Math.Abs(delta.Y) < PointerDrag.Threshold)
                return;

            // Capture here rather than on the press. A grip can sit inside a button — the
            // Performance rail's rows ARE buttons — and a button captures the pointer on its own
            // press, which would take this one straight back off us. Taking it on the first move
            // past the threshold instead cancels that click, which is what a drag should do.
            _dragging = true;
            _target.Begin(_host.SlotRects, _host.SlotRowEnds, IndexOfItem());
            device?.Capture(_host.Surface);
            _host.BeginPreview();
            _lifted?.Classes.Add("dragging");
            _item.ZIndex = 10;
            _hint = new DragDropHint(_host.Surface, _host.Surface.Radius("RadiusPanel", 4));
            _hint.Attach();
        }

        // Re-pack under the order being tried, so the others shift as the drag moves.
        if (_host.PreviewMove(_item, TargetSlot()))
            _host.Surface.InvalidateMeasure();
        _host.Surface.InvalidateArrange();
    }

    /// <summary>Where the dragged item belongs: the slot it is covering, which is where it already
    /// looks like it will land. Measured against the slots as they were when the drag began, so the
    /// preview cannot move the answer under a still pointer.</summary>
    private int TargetSlot() {
        // A child hidden, shown or removed mid-drag changes what the indices mean, so start over from
        // now. A generator adding one does not: the preview order never takes in new children.
        if (_target.SlotCount != _host.Items.Count)
            _target.Begin(_host.SlotRects, _host.SlotRowEnds, IndexOfItem());

        var center = Centre(DragBox());
        return Math.Clamp(_target.Update(center.X, center.Y), 0, Math.Max(0, _host.Items.Count - 1));
    }

    private int IndexOfItem() {
        for (var i = 0; i < _host.Items.Count; i++)
            if (ReferenceEquals(_host.Items[i], _item))
                return i;
        return -1;
    }

    // The middle of the item as drawn, not the pointer inside it. The pointer can be anywhere in the
    // item — half a card's width from its middle — so a card grabbed by its right edge and dragged
    // straight down used to reach the next column's half of the row before it looked anywhere near it.
    private static Point Centre(Rect box) => new(box.X + box.Width / 2, box.Y + box.Height / 2);

    private void OnReleased(object? sender, PointerReleasedEventArgs e) {
        // Only a drag that took the capture may release it. Releasing unconditionally strips the
        // capture a button took on its own press, and it then never sees the release that clicks it.
        if (!_dragging) {
            _pending = false;
            return;
        }

        _host.CommitPreview();
        e.Pointer.Capture(null);

        // This release ends a drag, not a click, so it must not also press whatever is under it.
        e.Handled = true;
        End();
    }

    private void OnCaptureLost(object? sender, PointerCaptureLostEventArgs e) {
        if (_dragging)
            End();
        _pending = false;
    }

    private void OnExited(object? sender, PointerEventArgs e) {
        if (!_dragging)
            ShowGripCursor(null);
    }

    /// <summary>Points the cursor at the handle under it, from the same predicate that starts a drag.
    /// Driving both from one place is what stops a panel advertising a drag it cannot do — a widget
    /// outside a board, or a control sitting inside a header, offers no handle.</summary>
    private void ShowGripCursor(Visual? source) {
        InputElement? grip = null;
        if (source is not null && _host.TryGetHandle(source, out var handle))
            grip = handle.Grip;

        if (ReferenceEquals(grip, _gripUnderPointer))
            return;

        if (_gripUnderPointer is not null)
            _gripUnderPointer.Cursor = null;
        if (grip is not null)
            grip.Cursor = GripCursor;
        _gripUnderPointer = grip;
    }

    // Reached from a release and from a lost capture alike, so the lifted state can never stick.
    private void End() {
        _hint?.Hide();
        _hint = null;
        _lifted?.Classes.Remove("dragging");
        if (_item is not null)
            _item.ZIndex = 0;

        _item = null;
        _lifted = null;
        _dragging = false;
        _pending = false;
        _host.Surface.InvalidateMeasure();
        _host.Surface.InvalidateArrange();
    }
}
