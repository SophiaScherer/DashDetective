using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using DashDetective.Shared;
using DashDetective.Shared.Controls;
using System;
using System.Linq;

namespace DashDetective.Tabs.Processes;

public partial class ProcessesView : UserControl {
    // The view model the focus request is currently wired to. Tracked because the page's DataContext
    // arrives after construction and is swapped as the shell hosts the page.
    private ProcessesViewModel? _viewModel;

    public ProcessesView() {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;

        // Tunnel, because every header cell is a button and would otherwise consume the press.
        HeaderColumns.AddHandler(PointerPressedEvent, OnHeaderPressed, RoutingStrategies.Tunnel);
        HeaderColumns.AddHandler(PointerMovedEvent, OnHeaderMoved, RoutingStrategies.Tunnel);
        HeaderColumns.AddHandler(PointerReleasedEvent, OnHeaderReleased, RoutingStrategies.Tunnel);
        HeaderColumns.AddHandler(PointerCaptureLostEvent, OnHeaderCaptureLost, RoutingStrategies.Tunnel);
    }

    // ----- Column reorder -----
    //
    // Dragging a header cell moves its column. Modelled on WidgetBoard's drag: tunneling handlers and
    // PointerDrag.Threshold before a press counts as a drag. It differs in ONE load-bearing way — the
    // capture is taken when the drag starts, not on the press. Capturing on the press would strip the
    // capture the header button takes for its own click, and every column would stop sorting.

    private ProcessColumnId _draggedColumn;
    private Point _pressPoint;
    private bool _dragPending;
    private bool _dragging;

    private void OnHeaderPressed(object? sender, PointerPressedEventArgs e) {
        if (_viewModel is null || !e.GetCurrentPoint(HeaderColumns).Properties.IsLeftButtonPressed)
            return;
        if (e.Source is not Visual source)
            return;

        var cell = source.GetSelfAndVisualAncestors().OfType<SortableColumnHeader>().FirstOrDefault();
        if (cell is null)
            return;

        var column = _viewModel.ColumnAt(Grid.GetColumn(cell));
        if (column == ProcessColumns.Pinned)
            return;

        _draggedColumn = column;
        _pressPoint = e.GetPosition(HeaderColumns);
        _dragPending = true;
    }

    private void OnHeaderMoved(object? sender, PointerEventArgs e) {
        if (!_dragPending || _viewModel is null)
            return;

        var point = e.GetPosition(HeaderColumns);
        if (!_dragging) {
            if (Math.Abs(point.X - _pressPoint.X) < PointerDrag.Threshold)
                return;

            // Taking the capture here cancels the header button's click, which is exactly right: the
            // user is dragging the column, not asking to sort by it.
            _dragging = true;
            e.Pointer.Capture(HeaderColumns);
        }

        // Move as the pointer travels, so the columns preview the result. The view model stays quiet
        // about it — only the release reports an order worth saving.
        _viewModel.MoveColumn(_draggedColumn, DropIndex(point.X));
    }

    private void OnHeaderReleased(object? sender, PointerReleasedEventArgs e) {
        // Only a drag this view started may release the capture. A plain click never took one, and
        // clearing it here would strip the capture the header button took to raise its own click.
        if (!_dragging) {
            _dragPending = false;
            return;
        }

        e.Pointer.Capture(null);
        // This release ends a drag, not a click, so it must not also sort by the column it landed on.
        e.Handled = true;
        _dragging = false;
        _dragPending = false;
        _viewModel?.CommitColumnOrder();
    }

    // Fires for the header button losing its capture to the drag as well, so it only ends the drag
    // when the capture that went is the one the drag itself took.
    private void OnHeaderCaptureLost(object? sender, PointerCaptureLostEventArgs e) {
        if (_dragging && !ReferenceEquals(e.Pointer.Captured, HeaderColumns)) {
            _dragging = false;
            _dragPending = false;
        }
    }

    /// <summary>The column position the pointer is over. Index 0 holds the pinned column, so it is
    /// never a drop target; a pointer past either end lands on the nearest column that is.</summary>
    private int DropIndex(double x) {
        var leftmost = (Index: 1, Edge: double.PositiveInfinity);
        var rightmost = (Index: 1, Edge: double.NegativeInfinity);

        foreach (var child in HeaderColumns.Children) {
            if (child is not Control cell)
                continue;

            var index = Grid.GetColumn(cell);
            if (index < 1)
                continue;

            if (x >= cell.Bounds.X && x < cell.Bounds.Right)
                return index;
            if (cell.Bounds.X < leftmost.Edge)
                leftmost = (index, cell.Bounds.X);
            if (cell.Bounds.Right > rightmost.Edge)
                rightmost = (index, cell.Bounds.Right);
        }

        return x < leftmost.Edge ? leftmost.Index : rightmost.Index;
    }

    private void OnDataContextChanged(object? sender, EventArgs e) {
        if (_viewModel is not null) {
            _viewModel.FilterFocusRequested -= FocusFilter;
            _viewModel.ScrollToTopRequested -= ScrollToTop;
        }

        _viewModel = DataContext as ProcessesViewModel;

        if (_viewModel is not null) {
            _viewModel.FilterFocusRequested += FocusFilter;
            _viewModel.ScrollToTopRequested += ScrollToTop;
        }
    }

    // A jump from universal search narrows the list to the process asked for; the table has to go back
    // to the top for the selected row to be on screen. The view owns the ScrollViewer, so it listens.
    private void ScrollToTop() => ProcessListScroll.ScrollToHome();

    // Focusing selects what's already typed, so a second "/" replaces the term rather than appending
    // to it — the behaviour every browser's find bar has.
    private void FocusFilter() => FilterBox.FocusAndSelectAll();

    // Tap selects the row (drives the highlight + End task / Properties enablement). Handled here
    // rather than in the view model because a row tap has no XAML command binding — the same pattern
    // as File Explorer's row selection.
    private void OnRowTapped(object? sender, TappedEventArgs e) {
        // A tap on the chevron expands/collapses (OnChevronClick) and must not also select the row —
        // the Tapped gesture bubbles from the button, so skip selection when it originated there.
        if (e.Source is Visual source &&
            source.GetSelfAndVisualAncestors().OfType<Button>().Any(b => b.Classes.Contains("chev")))
            return;
        if (sender is Control { DataContext: ProcessRow row } && DataContext is ProcessesViewModel vm)
            vm.SelectRow(row);
    }

    // The chevron expands/collapses a multi-process app's children. Handled here (like the row tap) as
    // it has no XAML command binding. Marked handled so it doesn't also select the row via the Border's
    // Tapped, keeping expand and select independent.
    private void OnChevronClick(object? sender, RoutedEventArgs e) {
        if (sender is Control { DataContext: ProcessRow row } && DataContext is ProcessesViewModel vm)
            vm.ToggleExpand(row);
        e.Handled = true;
    }

    // The native Properties dialog needs the owning window handle, so it's invoked here rather than
    // from the view model (the same reason the Export and File Explorer Properties dialogs live in
    // code-behind).
    private void OnPropertiesClick(object? sender, RoutedEventArgs e) {
        if (DataContext is not ProcessesViewModel { SelectedRow: { } row } vm)
            return;

        var handle = TopLevel.GetTopLevel(this)?.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        vm.ShowProperties(handle, row.Pid);
    }
}
