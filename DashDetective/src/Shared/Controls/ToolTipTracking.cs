using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using System;

namespace DashDetective.Shared.Controls;

/// <summary>
/// Moves an open tooltip with the cursor while the pointer stays over its owner. Attached app-wide from
/// SharedStyles.axaml. Dismissal stays the framework's: it already closes a tip once the pointer, or a layout
/// change under a still pointer, leaves the owner.
/// </summary>
public static class ToolTipTracking {
    /// <summary>
    /// The smallest offset change that re-places a tip. Avalonia re-reads the pointer whenever an open tip's
    /// offset changes, so a sub-pixel toggle moves the tip to the cursor and nowhere else.
    /// </summary>
    internal const double Nudge = 0.01;

    /// <summary>Set on a <see cref="TopLevel"/> to make the tooltips inside it follow the pointer.</summary>
    public static readonly AttachedProperty<bool> FollowsPointerProperty =
        AvaloniaProperty.RegisterAttached<TopLevel, bool>("FollowsPointer", typeof(ToolTipTracking));

    // Where the pointer last moved in this top level: the point Avalonia anchors a pointer-placed tip to.
    private static readonly AttachedProperty<Point> LastPointerProperty =
        AvaloniaProperty.RegisterAttached<TopLevel, Point>("LastPointer", typeof(ToolTipTracking));

    // The owner's own offsets before tracking began, so closing the tip hands them back exactly.
    private static readonly AttachedProperty<Vector?> OffsetBeforeTrackingProperty =
        AvaloniaProperty.RegisterAttached<Control, Vector?>("OffsetBeforeTracking", typeof(ToolTipTracking));

    // How many tips are open app-wide, so a move with none open skips the hit test.
    private static int _openTips;

    static ToolTipTracking() {
        FollowsPointerProperty.Changed.AddClassHandler<TopLevel>(OnFollowsPointerChanged);
        ToolTip.IsOpenProperty.Changed.AddClassHandler<Control>(OnTipOpenChanged);
    }

    public static bool GetFollowsPointer(TopLevel root) => root.GetValue(FollowsPointerProperty);

    public static void SetFollowsPointer(TopLevel root, bool value) => root.SetValue(FollowsPointerProperty, value);

    /// <summary>
    /// The control whose tip is open under the pointer: the nearest ancestor of <paramref name="hit"/> (itself
    /// included) that <paramref name="isOpen"/> accepts, or null when the pointer is over no open tip's owner.
    /// </summary>
    internal static Control? OpenOwner(Visual? hit, Func<Control, bool> isOpen) {
        for (var visual = hit; visual is not null; visual = visual.GetVisualParent())
            if (visual is Control control && isOpen(control))
                return control;
        return null;
    }

    /// <summary>The owner of the tip <paramref name="hit"/> sits inside, when the pointer is over a tip itself.</summary>
    internal static Control? TipOwner(Visual? hit) =>
        hit?.FindAncestorOfType<ToolTip>(includeSelf: true)?.GetLogicalParent() is Popup popup
            ? popup.PlacementTarget
            : null;

    /// <summary>
    /// The owner's next offsets: its own plus how far the pointer is from where the tip was anchored. Equal to
    /// the current ones only when nothing would move, so it steps a nudge aside to make Avalonia re-place it.
    /// </summary>
    internal static Vector Next(Vector current, Vector baseline, Vector shift) {
        var target = baseline + shift;
        return target == current ? target + new Vector(Nudge, 0) : target;
    }

    private static void OnFollowsPointerChanged(TopLevel root, AvaloniaPropertyChangedEventArgs e) {
        // Tunnelling with handledEventsToo sees every move, including over a control that consumes them.
        if (e.GetNewValue<bool>())
            root.AddHandler(InputElement.PointerMovedEvent, OnPointerMoved, RoutingStrategies.Tunnel,
                            handledEventsToo: true);
        else
            root.RemoveHandler(InputElement.PointerMovedEvent, OnPointerMoved);
    }

    private static void OnPointerMoved(object? sender, PointerEventArgs e) {
        if (sender is not TopLevel root)
            return;

        var point = e.GetPosition(root);
        root.SetValue(LastPointerProperty, point);
        if (_openTips == 0)
            return;

        // Hit-tested as the framework does, disabled controls included: a disabled owner can show its tip, but
        // its moves are routed from the nearest enabled ancestor.
        var hit = root.InputHitTest(point, enabledElementsOnly: false) as Visual ?? e.Source as Visual;

        if (OpenOwner(hit, ToolTip.GetIsOpen) is { } owner) {
            Track(owner, default);
            return;
        }

        // A quick move can land the pointer on the tip, whose own window then takes the moves. The tip keeps
        // following, shifted by the pointer's distance from the anchor; if the pointer has in fact left the
        // owner, the framework closes the tip on the next move.
        if (TipOwner(hit) is { } tipped && ToolTip.GetIsOpen(tipped) && TopLevel.GetTopLevel(tipped) is { } ownerRoot
            && ownerRoot != root && ownerRoot.PointToClient(root.PointToScreen(point)) is var there)
            Track(tipped, there - ownerRoot.GetValue(LastPointerProperty));
    }

    /// <summary>Re-places <paramref name="owner"/>'s open tip at the pointer, offset by <paramref name="shift"/>.
    /// Internal so a test can drive it without a pointer.</summary>
    internal static void Track(Control owner, Vector shift) {
        // A tip placed against its owner rather than the pointer is meant to stay put.
        if (ToolTip.GetPlacement(owner) != PlacementMode.Pointer)
            return;

        var current = new Vector(ToolTip.GetHorizontalOffset(owner), ToolTip.GetVerticalOffset(owner));
        var baseline = owner.GetValue(OffsetBeforeTrackingProperty) ?? current;
        var next = Next(current, baseline, shift);

        owner.SetValue(OffsetBeforeTrackingProperty, baseline);
        owner.SetCurrentValue(ToolTip.HorizontalOffsetProperty, next.X);
        owner.SetCurrentValue(ToolTip.VerticalOffsetProperty, next.Y);
    }

    private static void OnTipOpenChanged(Control owner, AvaloniaPropertyChangedEventArgs e) {
        var open = e.GetNewValue<bool>();
        _openTips = Math.Max(0, _openTips + (open ? 1 : -1));
        if (!open)
            Restore(owner);
    }

    /// <summary>Hands a closed tip's owner the offsets it had before tracking began. Internal so a test can
    /// reach it: a detached control cannot hold a tip open.</summary>
    internal static void Restore(Control owner) {
        if (owner.GetValue(OffsetBeforeTrackingProperty) is not { } original)
            return;

        owner.ClearValue(OffsetBeforeTrackingProperty);
        owner.SetCurrentValue(ToolTip.HorizontalOffsetProperty, original.X);
        owner.SetCurrentValue(ToolTip.VerticalOffsetProperty, original.Y);
    }
}
