using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.VisualTree;
using DashDetective.Shared;

namespace DashDetective.Shared.Layout;

/// <summary>
/// The accent band a drag draws where the thing being dragged will land. Lives in the window's
/// <see cref="OverlayLayer"/> rather than in the dragging control's own tree: a panel that reordered
/// its children to make room for a hint would re-enter its own layout, and the nav bar's hint has to
/// cover the shell.
///
/// Shared by the nav bar's drag-to-dock and the widget boards' drag-to-reorder, so the two read as
/// one gesture.
/// </summary>
public sealed class DragDropHint {
    private readonly Control _owner;
    private readonly CornerRadius _radius;
    private OverlayLayer? _overlay;
    private Border? _hint;

    /// <param name="owner">The control the drag belongs to; supplies the overlay and the theme.</param>
    /// <param name="radius">Corner rounding, to match the surface being previewed.</param>
    public DragDropHint(Control owner, double radius = 4) {
        _owner = owner;
        _radius = new CornerRadius(radius);
    }

    /// <summary>Claims the window's overlay layer and puts an empty hint in it, returning the layer so
    /// the caller can measure the drag in the same coordinates. Null when there is no overlay, which
    /// is the caller's signal not to start a drag it cannot preview.</summary>
    public OverlayLayer? Attach() {
        _overlay = OverlayLayer.GetOverlayLayer(_owner);
        if (_overlay is null)
            return null;

        var accent = _owner.BrushColor("Accent", Colors.DodgerBlue);
        _hint = new Border {
            Background = new SolidColorBrush(accent, 0.18),
            BorderBrush = new SolidColorBrush(accent),
            BorderThickness = new Thickness(2),
            CornerRadius = _radius,
            IsHitTestVisible = false,
            IsVisible = false,
        };
        _overlay.Children.Add(_hint);
        return _overlay;
    }

    /// <summary>Moves the hint to this box, in overlay coordinates.</summary>
    public void Show(Rect box) {
        if (_hint is null)
            return;

        Canvas.SetLeft(_hint, box.X);
        Canvas.SetTop(_hint, box.Y);
        _hint.Width = box.Width;
        _hint.Height = box.Height;
        _hint.IsVisible = true;
    }

    /// <summary>Moves the hint to this box, given in <paramref name="source"/>'s coordinates.</summary>
    public void ShowFrom(Visual source, Rect box) {
        if (_overlay is null || source.TranslatePoint(box.Position, _overlay) is not { } origin)
            return;
        Show(new Rect(origin, box.Size));
    }

    /// <summary>Takes the hint back out of the overlay. Reached from a release and from a lost capture
    /// alike, so a hint can never be left drawn over the window.</summary>
    public void Hide() {
        if (_overlay is not null && _hint is not null)
            _overlay.Children.Remove(_hint);
        _hint = null;
        _overlay = null;
    }
}
