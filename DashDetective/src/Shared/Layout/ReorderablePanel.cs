using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

    private readonly ReorderSlides _slides = new();
    private readonly Dictionary<Control, Point> _drawnAt = new();
    private bool _frameRequested;

    protected ReorderablePanel() {
        Drag = new ReorderDrag(this);
        MotionReduced = () => TopLevel.GetTopLevel(this)?.Classes.Contains("reduceMotion") == true;
        var clock = Stopwatch.StartNew();
        Clock = () => clock.Elapsed;
    }

    /// <summary>Whether Reduce motion is on: the shell puts the class on the window. A seam so a test,
    /// which has no window, can turn it on.</summary>
    internal Func<bool> MotionReduced { get; set; }

    /// <summary>The time slides are measured on. A seam so a test can step it.</summary>
    internal Func<TimeSpan> Clock { get; set; }

    public IReadOnlyList<string>? Order {
        get => GetValue(OrderProperty);
        set => SetValue(OrderProperty, value);
    }

    /// <summary>Raised with the new id order once a drag commits.</summary>
    public event Action<IReadOnlyList<string>>? OrderChanged;

    /// <summary>The pointer half of the drag, which a subclass reads as it lays out: the dragged item
    /// is measured at the size it was picked up at and arranged where it is being held.</summary>
    protected internal ReorderDrag Drag { get; }

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

    ReadOnlySpan<Rect2> IReorderablePanel.SlotRects => _slotRects.AsSpan(0, _visible.Count);

    ReadOnlySpan<int> IReorderablePanel.SlotRowEnds => CollectionsMarshal.AsSpan(RowEnds);

    /// <summary>Rebuilds <see cref="Visible"/> in the order to lay out; call it first thing in measure.
    /// Collapsed children are skipped, so a hidden one never takes a slot.</summary>
    protected void CollectVisible() {
        // A generator rebuilds its children whenever its source changes, which resets the order — so
        // put the saved one back before laying anything out.
        if (_childOrder.Sync(DeclaredIds()))
            ApplySavedOrder();

        _visible.Clear();
        foreach (var index in _childOrder.Shown(Drag.IsDragging))
            if (index < Children.Count && Children[index].IsVisible)
                _visible.Add(Children[index]);

        if (_slotRects.Length < _visible.Count)
            _slotRects = new Rect2[_visible.Count];

        // Forget where a child no longer on screen was drawn: one shown again appears, it does not slide.
        if (_drawnAt.Count > _visible.Count) {
            var gone = new List<Control>();
            foreach (var child in _drawnAt.Keys)
                if (!_visible.Contains(child))
                    gone.Add(child);
            foreach (var child in gone) {
                _drawnAt.Remove(child);
                StopSlide(child);
            }
        }

        // Every reorderable item is a tab stop, so the keyboard can reach one to move it. Done here
        // rather than per view because a card is usually a plain Border with no control inside: pages
        // whose items happened to contain a button were reachable, the rest were not reachable at all.
        if (Reorderable)
            foreach (var child in _visible)
                child.Focusable = true;
    }

    /// <summary>Device pixels per layout unit, as the framework's own rounding counts them; 0 where
    /// this panel does not round, which leaves every slot as computed.</summary>
    private double SnapScale => UseLayoutRounding ? LayoutHelper.GetLayoutScale(this) : 0;

    /// <summary>The width a child will be arranged at once its slot is snapped to pixels, so it can be
    /// measured at that width rather than one its arrange then disagrees with.</summary>
    protected double SnappedWidth(Control child, double x, double width, double panelWidth) =>
        PixelSnap.Span(x, width, Math.Max(child.MinWidth, Inner(child).MinWidth), panelWidth, SnapScale).Length;

    /// <summary>Snaps a slot to whole pixels, records it for drops, and gives back the box to arrange
    /// that child at: its slot, or for a dragged child wherever it is being held.</summary>
    protected Rect Placed(int index, Rect slot, Size panel) {
        var child = _visible[index];
        var inner = Inner(child);
        var scale = SnapScale;
        var (x, width) = PixelSnap.Span(slot.X, slot.Width, Math.Max(child.MinWidth, inner.MinWidth),
                                        panel.Width, scale);
        var (y, height) = PixelSnap.Span(slot.Y, slot.Height, Math.Max(child.MinHeight, inner.MinHeight),
                                         panel.Height, scale);
        slot = new Rect(x, y, width, height);

        _slotRects[index] = new Rect2(slot.X, slot.Y, slot.Width, slot.Height);
        if (!ReferenceEquals(child, Drag.Dragged)) {
            Slide(child, slot.Position);
            return slot;
        }

        // A held item is drawn at the pointer, so it must not also be sliding.
        StopSlide(child);
        Drag.ShowHint(slot);
        return Drag.DragBox();
    }

    /// <summary>Eases a child that moved into its new slot instead of jumping: the others as a drag
    /// re-packs them, and the dropped item from where it was let go. Nothing else moves it — a resize
    /// or a cancelled drag lands at once, and Reduce motion turns all of it off.</summary>
    private void Slide(Control child, Point slot) {
        var settle = Drag.TakeSettle(child);
        var moved = _drawnAt.TryGetValue(child, out var was) && was != slot;
        _drawnAt[child] = slot;

        if (MotionReduced()) {
            StopSlide(child);
            return;
        }

        var now = Clock();
        if (settle is { } from) {
            _slides.Start(child, from.X - slot.X, from.Y - slot.Y, now);
            child.ZIndex = 10;      // over the neighbours it passes on the way in
        } else if (moved && Drag.IsDragging) {
            _slides.Start(child, was.X - slot.X, was.Y - slot.Y, now);
        } else {
            if (moved)
                StopSlide(child);
            return;
        }

        Draw(child, now);
        RequestFrame();
    }

    private void StopSlide(Control child) {
        if (!_slides.IsSliding(child))
            return;

        _slides.Stop(child);
        child.RenderTransform = null;
        child.ZIndex = 0;
    }

    private void Draw(Control child, TimeSpan now) {
        var (x, y) = _slides.OffsetAt(child, now);
        if (child.RenderTransform is TranslateTransform shift) {
            shift.X = x;
            shift.Y = y;
        } else {
            child.RenderTransform = new TranslateTransform(x, y);
        }
    }

    private void RequestFrame() {
        if (_frameRequested || TopLevel.GetTopLevel(this) is not { } top)
            return;

        _frameRequested = true;
        top.RequestAnimationFrame(_ => OnFrame());
    }

    /// <summary>Advances every slide one frame. Internal so a test can step it without a render loop.</summary>
    internal void OnFrame() {
        _frameRequested = false;
        var now = Clock();
        foreach (var child in _slides.Finish(now)) {
            var control = (Control)child;
            control.RenderTransform = null;
            if (!ReferenceEquals(control, Drag.Dragged))
                control.ZIndex = 0;
        }

        foreach (var child in _slides.Items)
            Draw((Control)child, now);
        if (_slides.Count > 0)
            RequestFrame();
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
