using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Data;
using Avalonia.VisualTree;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace DashDetective.Shared.Layout;

/// <summary>
/// The half of drag-to-reorder every reorderable panel shares: the saved order and the re-entrancy
/// guard around it, the order being previewed, the children on screen and where their slots were
/// arranged, and the pointer work it hands to <see cref="ReorderDrag"/>. A subclass supplies the
/// layout — where the slots go, and what counts as a handle.
///
/// A base class rather than a helper each panel forwards to: the forwarding would be most of what
/// they share, and <see cref="Order"/> would have to become an attached property to live anywhere
/// else, which changes every call site's markup. The layout vocabulary stays with the panels; only
/// the reordering is here.
/// </summary>
public abstract class ReorderablePanel : Panel, IReorderablePanel {
    /// <summary>The item ids in display order, two-way so a drag reports its result back to the page
    /// that persists it. Ids the panel does not have are ignored; ones it has that are missing keep
    /// their declared position.</summary>
    public static readonly StyledProperty<IReadOnlyList<string>?> OrderProperty =
        AvaloniaProperty.Register<ReorderablePanel, IReadOnlyList<string>?>(
            nameof(Order), defaultBindingMode: BindingMode.TwoWay);

    private readonly List<Control> _visible = new();
    private readonly ChildOrder _childOrder = new();
    private Rect2[] _slotRects = Array.Empty<Rect2>();
    private bool _applyingOrder;

    protected ReorderablePanel() => Drag = new ReorderDrag(this);

    public IReadOnlyList<string>? Order {
        get => GetValue(OrderProperty);
        set => SetValue(OrderProperty, value);
    }

    /// <summary>Raised with the new id order once a drag commits.</summary>
    public event Action<IReadOnlyList<string>>? OrderChanged;

    /// <summary>The pointer half of the drag, which a subclass reads as it lays out: the dragged item
    /// is measured at the size it was picked up at and arranged where it is being held.</summary>
    protected ReorderDrag Drag { get; }

    /// <summary>The children on screen, in the order to lay them out in.</summary>
    protected IReadOnlyList<Control> Visible => _visible;

    /// <summary>The index just past each row, filled by the subclass as it measures. A drop reads it
    /// to find which row it is over.</summary>
    protected List<int> RowEnds { get; } = new();

    /// <summary>Whether a drag may start here at all. A board always reorders; a strip is a layout
    /// before it is a control, so it says no until its call site asks.</summary>
    protected virtual bool Reorderable => true;

    /// <summary>What this press may drag, if anything — the one rule a panel cannot share.</summary>
    public abstract bool TryGetHandle(Visual source, out ReorderHandle handle);

    /// <summary>Whether this child is one of the ones on screen. A press on a collapsed child, or on
    /// something that is not a child at all, is not a drag.</summary>
    protected bool IsShown(Control child) => _visible.Contains(child);

    /// <summary>What a child actually is. An ItemsControl hands the panel a ContentPresenter wrapped
    /// around the real item, and everything authored on that item — its slot properties, its grip, the
    /// picked-up look — belongs to what is inside the wrapper.</summary>
    protected static Control Inner(Control child) =>
        child is ContentPresenter { Child: Control content } ? content : child;

    public int SlotAt(Point point) =>
        WidgetBoardLayout.SlotAt(_slotRects.AsSpan(0, _visible.Count),
                                 CollectionsMarshal.AsSpan(RowEnds), point.X, point.Y);

    public void BeginPreview() => _childOrder.BeginPreview();

    public bool PreviewMove(Control item, int target) =>
        _childOrder.Move(Children.IndexOf(item), target, IsChildVisible);

    /// <summary>Moves the item the keyboard is on by one slot, reporting whether it went anywhere.
    /// Drag and keyboard share Begin/Preview/Commit, so both persist through one path.</summary>
    public bool TryMoveFocused(Visual? focused, int delta) {
        if (!Reorderable || focused is null)
            return false;

        var index = IndexOfContaining(focused);
        var target = index + delta;
        if (index < 0 || target < 0 || target >= _visible.Count)
            return false;

        BeginPreview();
        if (!PreviewMove(_visible[index], target))
            return false;

        CommitPreview();
        InvalidateMeasure();
        return true;
    }

    /// <summary>Which shown item contains the focus, or -1. Focus lands on something inside a widget —
    /// a fold chevron, a button in its body — never on the panel's own child.</summary>
    private int IndexOfContaining(Visual focused) {
        for (var v = focused; v is not null; v = v.GetVisualParent()) {
            var i = _visible.IndexOf((v as Control)!);
            if (i >= 0)
                return i;
        }
        return -1;
    }

    public void CommitPreview() {
        var ids = _childOrder.Commit(DeclaredIds());
        if (ids.Count == 0)
            return;

        _applyingOrder = true;
        Order = ids;
        _applyingOrder = false;
        OrderChanged?.Invoke(ids);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change) {
        base.OnPropertyChanged(change);
        if (change.Property != OrderProperty || _applyingOrder)
            return;

        // An empty order is a RESET, not "nothing to apply": ApplySaved deliberately leaves the
        // permutation in place, so the way back to the declared order has to be asked for.
        var moved = Order is { Count: > 0 } ? ApplySavedOrder() : _childOrder.Reset(Children.Count);
        if (moved)
            InvalidateMeasure();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e) {
        base.OnAttachedToVisualTree(e);
        if (Reorderable)
            Drag.Attach();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e) {
        if (Reorderable)
            Drag.Detach();
        base.OnDetachedFromVisualTree(e);
    }

    Panel IReorderablePanel.Surface => this;

    IReadOnlyList<Control> IReorderablePanel.Items => _visible;

    /// <summary>Rebuilds <see cref="Visible"/> in the order to lay out; call it first thing in measure.
    /// Collapsed children are skipped, so a hidden one never takes a slot.</summary>
    protected void CollectVisible() {
        // A generator rebuilds its children whenever its source changes, which resets the order — so
        // put the saved one back before laying anything out.
        if (_childOrder.Sync(Children.Count))
            ApplySavedOrder();

        _visible.Clear();
        foreach (var index in _childOrder.Shown(Drag.IsDragging))
            if (index < Children.Count && Children[index].IsVisible)
                _visible.Add(Children[index]);

        if (_slotRects.Length < _visible.Count)
            _slotRects = new Rect2[_visible.Count];
    }

    /// <summary>Records where a slot was arranged — which is what a drop is measured against — and
    /// gives back the box to arrange that child at. Normally its own slot; for the one being dragged,
    /// wherever it is being held, with the slot it would land in outlined behind it.</summary>
    protected Rect Placed(int index, Rect slot) {
        _slotRects[index] = new Rect2(slot.X, slot.Y, slot.Width, slot.Height);
        if (!ReferenceEquals(_visible[index], Drag.Dragged))
            return slot;

        Drag.ShowHint(slot);
        return Drag.DragBox();
    }

    private bool ApplySavedOrder() => _childOrder.ApplySaved(Order, DeclaredIds());

    private bool IsChildVisible(int index) => index < Children.Count && Children[index].IsVisible;

    private List<string> DeclaredIds() {
        var ids = new List<string>(Children.Count);
        foreach (var child in Children)
            ids.Add(Reorder.IdOf(child));
        return ids;
    }
}
