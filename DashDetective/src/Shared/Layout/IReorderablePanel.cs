using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using System.Collections.Generic;

namespace DashDetective.Shared.Layout;

/// <summary>
/// What a press on a reorderable panel found. Three controls rather than one, because they are rarely
/// the same element: a Network console panel is the child the board lays out, the WidgetPanel inside
/// it is what wears the picked-up look, and only its header is the part you may grab.
/// </summary>
/// <param name="Item">The panel's own child, the thing that moves.</param>
/// <param name="Lifted">What the <c>dragging</c> class goes on.</param>
/// <param name="Grip">The handle: what the drag cursor goes on, and the only part a press may start
/// a drag from.</param>
public readonly record struct ReorderHandle(Control Item, Control Lifted, InputElement Grip);

/// <summary>
/// A panel whose children a <see cref="ReorderDrag"/> can reorder. The panel owns the layout — where
/// each child sits, and what counts as a handle — and the drag owns the pointer.
///
/// Reordering is always a permutation of the panel's own index list. A panel must never reorder
/// <c>Children</c> to do it: that is re-entrant mid-layout, detaches a live chart from its feed, and
/// for an ItemsControl panel fights the item generator.
/// </summary>
public interface IReorderablePanel {
    /// <summary>The panel itself, for pointer capture, coordinates and the overlay layer.</summary>
    Panel Surface { get; }

    /// <summary>The children on screen, in the order currently shown.</summary>
    IReadOnlyList<Control> Items { get; }

    /// <summary>What this press may drag, if anything.</summary>
    bool TryGetHandle(Visual source, out ReorderHandle handle);

    /// <summary>The slot the drag is over, as an index into <see cref="Items"/>. The dragged item
    /// takes that slot; it is not inserted beside it.</summary>
    int SlotAt(Point point);

    /// <summary>Starts previewing a reorder: the shown order becomes the one being tried.</summary>
    void BeginPreview();

    /// <summary>Moves the dragged item to this index in the previewed order. False when it is already
    /// there, so a wobble does not re-pack every frame.</summary>
    bool PreviewMove(Control item, int target);

    /// <summary>Keeps the previewed order and reports it to whatever persists it.</summary>
    void CommitPreview();
}
