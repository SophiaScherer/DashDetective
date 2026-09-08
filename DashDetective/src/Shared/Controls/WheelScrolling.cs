using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using DashDetective.Services.Input;
using System;

namespace DashDetective.Shared.Controls;

/// <summary>
/// Steps a <see cref="ScrollViewer"/> by the OS's lines-per-notch setting instead of Avalonia's fixed
/// 50 px. Attached app-wide from SharedStyles.axaml, so scrollers inside control templates follow it too.
///
/// It extends the toolkit's scroll rather than replacing it: the presenter's handler is a class handler
/// on a descendant and cannot be preempted, so chaining and clamping stay its business.
/// </summary>
public static class WheelScrolling {
    private static readonly IWheelScrollLines SystemLines = IWheelScrollLines.ForCurrentPlatform();

    /// <summary>Set on a <see cref="ScrollViewer"/> to make its wheel follow the OS setting.</summary>
    public static readonly AttachedProperty<bool> FollowsSystemProperty =
        AvaloniaProperty.RegisterAttached<ScrollViewer, bool>("FollowsSystem", typeof(WheelScrolling));

    // Where the surface sat before the toolkit moved it, so the notch is measured from one place.
    private static readonly AttachedProperty<Vector> OffsetBeforeWheelProperty =
        AvaloniaProperty.RegisterAttached<ScrollViewer, Vector>("OffsetBeforeWheel", typeof(WheelScrolling));

    static WheelScrolling() {
        FollowsSystemProperty.Changed.AddClassHandler<ScrollViewer>(OnFollowsSystemChanged);
    }

    public static bool GetFollowsSystem(ScrollViewer scroller) => scroller.GetValue(FollowsSystemProperty);

    public static void SetFollowsSystem(ScrollViewer scroller, bool value) =>
        scroller.SetValue(FollowsSystemProperty, value);

    private static void OnFollowsSystemChanged(ScrollViewer scroller, AvaloniaPropertyChangedEventArgs e) {
        if (e.GetNewValue<bool>()) {
            // Tunnelling reaches the surface before the presenter's handler, bubbling after it.
            scroller.AddHandler(InputElement.PointerWheelChangedEvent, OnWheelTunnel, RoutingStrategies.Tunnel);
            scroller.AddHandler(InputElement.PointerWheelChangedEvent, OnWheelBubble, RoutingStrategies.Bubble,
                                handledEventsToo: true);
        } else {
            scroller.RemoveHandler(InputElement.PointerWheelChangedEvent, OnWheelTunnel);
            scroller.RemoveHandler(InputElement.PointerWheelChangedEvent, OnWheelBubble);
        }
    }

    private static void OnWheelTunnel(object? sender, PointerWheelEventArgs e) {
        if (sender is ScrollViewer scroller)
            scroller.SetValue(OffsetBeforeWheelProperty, scroller.Offset);
    }

    private static void OnWheelBubble(object? sender, PointerWheelEventArgs e) {
        if (sender is not ScrollViewer scroller)
            return;

        var before = scroller.GetValue(OffsetBeforeWheelProperty);
        var moved = scroller.Offset - before;

        // Untouched means an inner surface took the notch, or this one is at that end. Stepping it anyway
        // would break the hand-off to the page.
        if (moved == default)
            return;

        var delta = Delta(e);
        var extent = scroller.Extent;
        var viewport = scroller.Viewport;
        var (unitX, unitY) = Units(scroller);
        var lines = SystemLines.PerNotch();

        var target = new Vector(
            moved.X == 0 ? before.X
                         : Clamp(before.X - delta.X * WheelStep.Distance(lines, viewport.Width, unitX),
                                 extent.Width - viewport.Width),
            moved.Y == 0 ? before.Y
                         : Clamp(before.Y - delta.Y * WheelStep.Distance(lines, viewport.Height, unitY),
                                 extent.Height - viewport.Height));

        if (target != scroller.Offset)
            scroller.SetCurrentValue(ScrollViewer.OffsetProperty, target);
    }

    /// <summary>A line in the surface's own units: one item where it scrolls by item, pixels otherwise.</summary>
    private static (double X, double Y) Units(ScrollViewer scroller) =>
        scroller.Presenter?.Child is ILogicalScrollable { IsLogicalScrollEnabled: true } logical
            ? (logical.ScrollSize.Width, logical.ScrollSize.Height)
            : (WheelStep.LineHeight, WheelStep.LineHeight);

    /// <summary>Shift+wheel means sideways, which some platforms send as a vertical delta.</summary>
    private static Vector Delta(PointerWheelEventArgs e) =>
        e.KeyModifiers == KeyModifiers.Shift && e.Delta.X == 0
            ? new Vector(e.Delta.Y, e.Delta.X)
            : e.Delta;

    private static double Clamp(double offset, double max) => Math.Clamp(offset, 0, Math.Max(max, 0));
}
