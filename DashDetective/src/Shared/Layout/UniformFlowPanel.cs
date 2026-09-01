using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.VisualTree;
using System;
using System.Collections.Generic;

namespace DashDetective.Shared.Layout;

/// <summary>What part of an item a drag may start from. A strip is a layout before it is a control,
/// so <see cref="None"/> is the default and reordering is asked for.</summary>
public enum ReorderGrip {
    /// <summary>Not reorderable.</summary>
    None,

    /// <summary>Anywhere on the item, minus any control inside it that takes clicks of its own.</summary>
    Item,

    /// <summary>Only from an element marked <see cref="Reorder.IsGripProperty"/>. For an item that is
    /// itself a control, so a press anywhere else belongs to that control.</summary>
    Marked,
}

/// <summary>
/// Lays children out in equal-width columns that wrap to a new row rather than shrinking past
/// <see cref="MinItemWidth"/>. Replaces a <c>UniformGrid</c> with a hardcoded <c>Columns</c>, which
/// squeezes its cells instead of reflowing. Column count comes from <see cref="FlowLayout"/>.
///
/// The panel owns the gutter via <see cref="ColumnSpacing"/> / <see cref="RowSpacing"/>, so call
/// sites drop the negative-margin-on-panel idiom — that cancellation assumes a fixed column count
/// and stops working once the count varies.
///
/// With <see cref="DragGrip"/> set it also drags to reorder, through the same
/// <see cref="ReorderDrag"/> a <see cref="WidgetBoard"/> uses. Unlike a board, its children are
/// usually generated from a collection, so the ids come off the item view models and the order is
/// re-applied whenever the generator rebuilds them.
/// </summary>
public class UniformFlowPanel : Panel, IReorderablePanel {
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

    /// <summary>What may start a drag here. Read once, when the panel enters the tree — set it in
    /// markup, not from a binding that changes later.</summary>
    public static readonly StyledProperty<ReorderGrip> DragGripProperty =
        AvaloniaProperty.Register<UniformFlowPanel, ReorderGrip>(nameof(DragGrip));

    /// <summary>The item ids in display order, two-way so a drag reports its result back to the page
    /// that persists it. Ids the panel does not have are ignored; ones it has that are missing keep
    /// their declared position.</summary>
    public static readonly StyledProperty<IReadOnlyList<string>?> OrderProperty =
        AvaloniaProperty.Register<UniformFlowPanel, IReadOnlyList<string>?>(
            nameof(Order), defaultBindingMode: BindingMode.TwoWay);

    private readonly List<Control> _visible = new();
    private readonly List<double> _rowHeights = new();
    private readonly List<int> _rowEnds = new();
    private readonly ChildOrder _childOrder = new();
    private readonly ReorderDrag _drag;
    private Rect2[] _slotRects = Array.Empty<Rect2>();
    private int _columns = 1;
    private bool _applyingOrder;

    static UniformFlowPanel() {
        AffectsMeasure<UniformFlowPanel>(MinItemWidthProperty, MaxColumnsProperty,
                                         ColumnSpacingProperty, RowSpacingProperty);
    }

    public UniformFlowPanel() => _drag = new ReorderDrag(this);

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

    public ReorderGrip DragGrip {
        get => GetValue(DragGripProperty);
        set => SetValue(DragGripProperty, value);
    }

    public IReadOnlyList<string>? Order {
        get => GetValue(OrderProperty);
        set => SetValue(OrderProperty, value);
    }

    /// <summary>Raised with the new id order once a drag commits.</summary>
    public event Action<IReadOnlyList<string>>? OrderChanged;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change) {
        base.OnPropertyChanged(change);
        if (change.Property == OrderProperty && !_applyingOrder && ApplySavedOrder())
            InvalidateMeasure();
    }

    // ----- Drag to reorder: the panel's half of it. The pointer is ReorderDrag's. -----

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e) {
        base.OnAttachedToVisualTree(e);
        if (DragGrip != ReorderGrip.None)
            _drag.Attach();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e) {
        if (DragGrip != ReorderGrip.None)
            _drag.Detach();
        base.OnDetachedFromVisualTree(e);
    }

    Panel IReorderablePanel.Surface => this;

    IReadOnlyList<Control> IReorderablePanel.Items => _visible;

    /// <summary>The whole item is the handle, minus anything inside it that takes clicks of its own —
    /// a press on a button in a card is aimed at the button.</summary>
    public bool TryGetHandle(Visual source, out ReorderHandle handle) {
        handle = default;
        if (DragGrip == ReorderGrip.None)
            return false;

        Control? marked = null;
        foreach (var node in source.GetSelfAndVisualAncestors()) {
            if (node is Control candidate && ReferenceEquals(candidate.GetVisualParent(), this)) {
                if (!_visible.Contains(candidate) || (DragGrip == ReorderGrip.Marked && marked is null))
                    return false;

                handle = new ReorderHandle(candidate, Lifted(candidate), marked ?? candidate);
                return true;
            }

            if (node is Control element && Reorder.GetIsGrip(element))
                marked = element;

            // Below the item root, so this is a control the press belongs to rather than the card. A
            // marked grip has already said which part is draggable, so it needs no such rule.
            if (DragGrip == ReorderGrip.Item && node is Button or ToggleButton or TextBox or ComboBox or ScrollBar)
                return false;
        }

        return false;
    }

    // A generated item is a ContentPresenter wrapped around the card, and the picked-up look belongs
    // on the card itself — a class on the presenter matches no style.
    private static Control Lifted(Control item) =>
        item is ContentPresenter { Child: Control card } ? card : item;

    public int SlotAt(Point point) =>
        WidgetBoardLayout.SlotAt(
            _slotRects.AsSpan(0, _visible.Count),
            System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_rowEnds),
            point.X, point.Y);

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

    protected override Size MeasureOverride(Size availableSize) {
        CollectVisible();
        _rowHeights.Clear();
        _rowEnds.Clear();
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

            // The dragged item keeps the width it was picked up at, so its content cannot re-wrap
            // under the cursor while the columns behind it reflow.
            var childWidth = ReferenceEquals(child, _drag.Dragged) ? _drag.DragSize.Width : measureWidth;
            child.Measure(new Size(childWidth, double.PositiveInfinity));
            rowHeight = Math.Max(rowHeight, child.DesiredSize.Height);
            widest = Math.Max(widest, child.DesiredSize.Width);
            if ((i + 1) % _columns == 0) {
                _rowHeights.Add(rowHeight);
                _rowEnds.Add(i + 1);
                rowHeight = 0;
            }
        }
        if (_visible.Count % _columns != 0) {
            _rowHeights.Add(rowHeight);
            _rowEnds.Add(_visible.Count);
        }

        if (unconstrained)
            itemWidth = widest;

        var height = 0.0;
        foreach (var h in _rowHeights)
            height += h;
        height += RowSpacing * Math.Max(0, _rowHeights.Count - 1);

        var totalWidth = itemWidth * _columns + ColumnSpacing * (_columns - 1);
        return new Size(totalWidth, height);
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
                var box = new Rect(x, y, itemWidth, rowHeight);
                _slotRects[index] = new Rect2(box.X, box.Y, box.Width, box.Height);

                // The dragged item follows the pointer instead, and the slot it left is what the drop
                // hint outlines. See WidgetBoard for why it is anchored to the pointer.
                if (ReferenceEquals(_visible[index], _drag.Dragged)) {
                    _drag.ShowHint(box);
                    box = _drag.DragBox();
                }

                _visible[index].Arrange(box);
            }
            y += rowHeight + RowSpacing;
        }

        return finalSize;
    }

    /// <summary>Collapsed children are skipped entirely so one never occupies a column.</summary>
    private void CollectVisible() {
        // The generator rebuilds these whenever its source changes, which resets the order — so put
        // the saved one back before laying anything out.
        if (_childOrder.Sync(Children.Count))
            ApplySavedOrder();

        _visible.Clear();
        foreach (var index in _childOrder.Shown(_drag.IsDragging))
            if (index < Children.Count && Children[index].IsVisible)
                _visible.Add(Children[index]);

        if (_slotRects.Length < _visible.Count)
            _slotRects = new Rect2[_visible.Count];
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
