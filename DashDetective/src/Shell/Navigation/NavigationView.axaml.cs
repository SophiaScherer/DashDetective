using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;

namespace DashDetective.Shell.Navigation;

/// <summary>
/// The shell's navigation bar. A self-contained component bound to a <see cref="NavigationViewModel"/>;
/// embedded directly by the shell rather than routed through the <c>ViewLocator</c>.
///
/// Also hosts the drag-to-dock gesture: pressing and dragging the brand area re-docks the bar to the
/// nearest window edge, with a highlight of the target edge drawn into the window's overlay layer. The
/// whole feature lives here so the shell needs no changes; on release it just asks the view model to
/// dock, reusing the same orientation path as the picker.
/// </summary>
public partial class NavigationView : UserControl {
    private NavigationViewModel? _viewModel;

    // Drag-to-dock state.
    private const double DragThreshold = 6;      // px of movement before a press becomes a drag
    private bool _dragPending;                   // pointer is down on the brand, not yet a drag
    private bool _dragging;                      // past the threshold: previewing/committing a dock
    private Point _pressPoint;                   // press location, in overlay coordinates
    private NavOrientation _targetEdge;          // edge the drop would dock to
    private OverlayLayer? _overlay;              // window-level layer the highlight is drawn into
    private Border? _dropHint;                   // the target-edge highlight

    public NavigationView() {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    // Bridge the view model's UI-only PositionPicked signal to dismissing the picker flyout: selecting
    // a dock position (or the current one) closes the menu, matching normal menu behaviour. Click-off
    // dismissal is already handled by the flyout's light-dismiss.
    private void OnDataContextChanged(object? sender, EventArgs e) {
        if (_viewModel is not null)
            _viewModel.PositionPicked -= ClosePositionFlyout;

        _viewModel = DataContext as NavigationViewModel;

        if (_viewModel is not null)
            _viewModel.PositionPicked += ClosePositionFlyout;
    }

    private void ClosePositionFlyout() => PositionButton.Flyout?.Hide();

    // ----- Drag-to-dock -----

    private void OnBrandPointerPressed(object? sender, PointerPressedEventArgs e) {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        _overlay = OverlayLayer.GetOverlayLayer(this);
        if (_overlay is null)
            return;

        _pressPoint = e.GetPosition(_overlay);
        _dragPending = true;
        e.Pointer.Capture((IInputElement)sender!);
    }

    private void OnBrandPointerMoved(object? sender, PointerEventArgs e) {
        if (!_dragPending || _overlay is null)
            return;

        var point = e.GetPosition(_overlay);

        if (!_dragging) {
            var delta = point - _pressPoint;
            if (Math.Abs(delta.X) < DragThreshold && Math.Abs(delta.Y) < DragThreshold)
                return;
            BeginDrag();
        }

        _targetEdge = NearestEdge(point, _overlay.Bounds.Size);
        UpdateHint(_targetEdge, _overlay.Bounds.Size);
    }

    private void OnBrandPointerReleased(object? sender, PointerReleasedEventArgs e) {
        if (_dragging)
            _viewModel?.DockTo(_targetEdge);

        e.Pointer.Capture(null);
        EndDrag();
    }

    private void OnBrandPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e) => EndDrag();

    private void BeginDrag() {
        _dragging = true;

        var accent = Colors.DodgerBlue;
        if (this.TryGetResource("Accent", ActualThemeVariant, out var res) && res is ISolidColorBrush brush)
            accent = brush.Color;

        _dropHint = new Border {
            Background = new SolidColorBrush(accent, 0.18),
            BorderBrush = new SolidColorBrush(accent),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(4),
            IsHitTestVisible = false,
        };
        _overlay?.Children.Add(_dropHint);
    }

    private void EndDrag() {
        if (_dropHint is not null && _overlay is not null)
            _overlay.Children.Remove(_dropHint);

        _dropHint = null;
        _overlay = null;
        _dragging = false;
        _dragPending = false;
    }

    // Nearest window edge to the pointer — every edge is a valid dock target, so snap to the closest.
    private static NavOrientation NearestEdge(Point p, Size size) {
        var distances = new (double Dist, NavOrientation Edge)[] {
            (p.X, NavOrientation.Left),
            (size.Width - p.X, NavOrientation.Right),
            (p.Y, NavOrientation.Top),
            (size.Height - p.Y, NavOrientation.Bottom),
        };

        var nearest = distances[0];
        foreach (var candidate in distances)
            if (candidate.Dist < nearest.Dist)
                nearest = candidate;

        return nearest.Edge;
    }

    // Size the highlight to the band the bar will occupy on the target edge, honouring the current
    // collapsed state so the preview matches where the bar actually lands.
    private void UpdateHint(NavOrientation edge, Size size) {
        if (_dropHint is null)
            return;

        var collapsed = _viewModel?.IsCollapsed ?? false;
        double vertical = collapsed ? 64 : 236;   // rail width on a Left/Right dock
        double horizontal = collapsed ? 54 : 64;   // bar height on a Top/Bottom dock

        double left, top, width, height;
        switch (edge) {
            case NavOrientation.Left:
                left = 0; top = 0; width = vertical; height = size.Height; break;
            case NavOrientation.Right:
                left = size.Width - vertical; top = 0; width = vertical; height = size.Height; break;
            case NavOrientation.Top:
                left = 0; top = 0; width = size.Width; height = horizontal; break;
            default: // Bottom
                left = 0; top = size.Height - horizontal; width = size.Width; height = horizontal; break;
        }

        Canvas.SetLeft(_dropHint, left);
        Canvas.SetTop(_dropHint, top);
        _dropHint.Width = width;
        _dropHint.Height = height;
    }
}
