using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace DashDetective.Tabs.Settings;

/// <summary>
/// What the color wheel and the brightness bar share: drag-to-pick with pointer capture, arrow keys with
/// Shift for coarse steps, and a thumb and focus ring drawn in black and white so they read on any color.
/// </summary>
public abstract class ColorPickSurface : Control {
    /// <summary>The thumb's radius, which is also how far the picking area is inset from the edge.</summary>
    protected const double ThumbRadius = 7;

    private static readonly IPen ThumbOuter = new ImmutablePen(Brushes.Black, 3.5);
    private static readonly IPen ThumbInner = new ImmutablePen(Brushes.White, 2);

    private bool _dragging;

    /// <summary>True while focus arrived by keyboard, the only time a focus ring is drawn.</summary>
    protected bool ShowsFocus { get; private set; }

    static ColorPickSurface() {
        FocusableProperty.OverrideDefaultValue<ColorPickSurface>(true);
    }

    protected ColorPickSurface() {
        Cursor = new Cursor(StandardCursorType.Hand);
    }

    /// <summary>Takes the value under a point in local coordinates.</summary>
    protected abstract void PickAt(Point point);

    /// <summary>Handles an arrow or Home/End press; false leaves the key to the rest of the app.</summary>
    protected abstract bool Nudge(Key key, bool coarse);

    protected static void DrawThumb(DrawingContext context, Point at) {
        context.DrawEllipse(null, ThumbOuter, at, ThumbRadius, ThumbRadius);
        context.DrawEllipse(null, ThumbInner, at, ThumbRadius, ThumbRadius);
    }

    /// <summary>The focus ring's two pens, outer then inner.</summary>
    protected static (IPen Outer, IPen Inner) FocusPens => (ThumbOuter, ThumbInner);

    protected override void OnPointerPressed(PointerPressedEventArgs e) {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        _dragging = true;
        e.Pointer.Capture(this);
        Focus();
        PickAt(e.GetPosition(this));
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e) {
        base.OnPointerMoved(e);
        if (_dragging)
            PickAt(e.GetPosition(this));
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e) {
        base.OnPointerReleased(e);
        if (!_dragging)
            return;

        _dragging = false;
        e.Pointer.Capture(null);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e) {
        base.OnPointerCaptureLost(e);
        _dragging = false;
    }

    protected override void OnKeyDown(KeyEventArgs e) {
        // Ctrl, Alt and Meta chords belong to the app's shortcuts, not to a nudge.
        var chord = (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Meta)) != 0;
        if (!chord && Nudge(e.Key, e.KeyModifiers.HasFlag(KeyModifiers.Shift)))
            e.Handled = true;
        else
            base.OnKeyDown(e);
    }

    protected override void OnGotFocus(FocusChangedEventArgs e) {
        base.OnGotFocus(e);
        ShowsFocus = e.NavigationMethod is NavigationMethod.Tab or NavigationMethod.Directional;
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e) {
        base.OnLostFocus(e);
        ShowsFocus = false;
        InvalidateVisual();
    }
}
