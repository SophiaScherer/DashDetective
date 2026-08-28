using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
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

        // Same reason: the rows carry their own tap gesture, which would otherwise eat the press.
        ProcessListScroll.AddHandler(PointerPressedEvent, OnListPressed, RoutingStrategies.Tunnel);
        ProcessListScroll.AddHandler(PointerMovedEvent, OnListMoved, RoutingStrategies.Tunnel);
        ProcessListScroll.AddHandler(PointerReleasedEvent, OnListReleased, RoutingStrategies.Tunnel);
        ProcessListScroll.AddHandler(PointerCaptureLostEvent, OnListCaptureLost, RoutingStrategies.Tunnel);

        OptionsPopup.Opened += OnOptionsPopupOpened;
        OptionsPopup.Closed += OnOptionsPopupClosed;
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
    // as File Explorer's row selection. Ctrl adds or removes the one row, Shift takes the run from the
    // last row clicked.
    private void OnRowTapped(object? sender, TappedEventArgs e) {
        // The chevron and the checkbox own their own gestures, and the Tapped bubbles up from both, so
        // a tap that started in either must not also re-select the row.
        if (OwnsItsOwnGesture(e.Source as Visual))
            return;
        if (sender is Control { DataContext: ProcessRow row } && DataContext is ProcessesViewModel vm)
            vm.SelectRow(row,
                         extend: e.KeyModifiers.HasFlag(KeyModifiers.Control),
                         range: e.KeyModifiers.HasFlag(KeyModifiers.Shift));
    }

    // The row's checkbox adds or removes just that row. The view model decides and the binding pushes
    // the answer back, so the box's own toggle never becomes the truth.
    private void OnRowCheckClick(object? sender, RoutedEventArgs e) {
        if (sender is Control { DataContext: ProcessRow row } && DataContext is ProcessesViewModel vm)
            vm.ToggleSelected(row);
        e.Handled = true;
    }

    // A group header's three-state box selects the whole group, or clears it when it already holds all
    // of it. The decision reads the view model rather than the box, whose own state has already
    // advanced by the time this runs.
    private void OnGroupCheckClick(object? sender, RoutedEventArgs e) {
        if (sender is Button { Tag: ProcessCategory category } && DataContext is ProcessesViewModel vm)
            vm.SetGroupSelected(category, !vm.IsGroupFullySelected(category));
        e.Handled = true;
    }

    /// <summary>Whether a pointer landed on something inside the row that handles its own click — the
    /// expand chevron or the selection checkbox.</summary>
    private static bool OwnsItsOwnGesture(Visual? source) {
        if (source is null)
            return false;

        foreach (var node in source.GetSelfAndVisualAncestors()) {
            if (node is Button button && (button.Classes.Contains("chev") || button.Classes.Contains("checkBox")))
                return true;
            if (node is Border border && border.Classes.Contains("procRow"))
                return false;
        }

        return false;
    }

    // ----- Drag to select a range -----
    //
    // Press on a row and drag down the list to take the run between them. Same shape as the column
    // drag: tunneling handlers, the capture taken only once the movement is a real drag, and released
    // only if this view took it.

    private int _rangePressPid;
    private Point _rangePressPoint;
    private bool _rangePending;
    private bool _rangeDragging;

    private void OnListPressed(object? sender, PointerPressedEventArgs e) {
        if (DataContext is not ProcessesViewModel vm)
            return;
        if (OwnsItsOwnGesture(e.Source as Visual) || RowAt(e.Source as Visual) is not { } row)
            return;

        var point = e.GetCurrentPoint(ProcessListScroll);

        // A right-click opens the shared row menu, whose items act on the page's selection — so the row
        // under the pointer has to be in it. Right-clicking inside an existing multi-selection leaves
        // that selection alone; anywhere else replaces it with the row clicked. Done on the press, which
        // tunnels, so it lands before the flyout opens.
        if (point.Properties.IsRightButtonPressed) {
            if (!row.IsSelected)
                vm.SelectRow(row);
            return;
        }

        if (!point.Properties.IsLeftButtonPressed)
            return;

        _rangePressPid = row.Pid;
        _rangePressPoint = point.Position;
        _rangePending = true;
    }

    private void OnListMoved(object? sender, PointerEventArgs e) {
        if (!_rangePending || DataContext is not ProcessesViewModel vm)
            return;

        var point = e.GetPosition(ProcessListScroll);
        if (!_rangeDragging) {
            if (Math.Abs(point.Y - _rangePressPoint.Y) < PointerDrag.Threshold)
                return;

            // Taking the capture here cancels the row's own tap, which is what a drag should do.
            _rangeDragging = true;
            e.Pointer.Capture(ProcessListScroll);
        }

        if (RowAt(ProcessListScroll.InputHitTest(point) as Visual) is { } row)
            vm.SelectRange(_rangePressPid, row.Pid);
    }

    private void OnListReleased(object? sender, PointerReleasedEventArgs e) {
        // A plain click never took a capture; clearing one here would strip whatever did take it.
        if (!_rangeDragging) {
            _rangePending = false;
            return;
        }

        e.Pointer.Capture(null);
        e.Handled = true;
        _rangeDragging = false;
        _rangePending = false;
    }

    // Also fires for the row losing its capture to the drag, so it only ends the drag when the capture
    // that went is the one the drag itself took.
    private void OnListCaptureLost(object? sender, PointerCaptureLostEventArgs e) {
        if (_rangeDragging && !ReferenceEquals(e.Pointer.Captured, ProcessListScroll)) {
            _rangeDragging = false;
            _rangePending = false;
        }
    }

    /// <summary>The row a visual sits in, or null when it sits in none.</summary>
    private static ProcessRow? RowAt(Visual? source) {
        if (source is null)
            return null;

        foreach (var node in source.GetSelfAndVisualAncestors())
            if (node is Border { DataContext: ProcessRow row } border && border.Classes.Contains("procRow"))
                return row;

        return null;
    }

    // Double-clicking anywhere on a row does what its chevron does — the gesture a tree is expected to
    // answer. A row with no children ignores it (ToggleExpand's own guard). The single taps that make up
    // the double still select, which is what the user sees happen first.
    private void OnRowDoubleTapped(object? sender, TappedEventArgs e) {
        if (OwnsItsOwnGesture(e.Source as Visual))
            return;
        if (sender is Control { DataContext: ProcessRow row } && DataContext is ProcessesViewModel vm)
            vm.ToggleExpand(row);
    }

    // A group heading folds its section shut. The rows stay where they are — only the list is hidden —
    // so the heading's count and its select-all box keep meaning the same thing.
    private void OnGroupToggleClick(object? sender, RoutedEventArgs e) {
        if (sender is Button { Tag: ProcessCategory category } && DataContext is ProcessesViewModel vm)
            vm.ToggleGroup(category);
        e.Handled = true;
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
    // code-behind). It shows the shell property sheet for one executable, so it acts on the primary
    // row even when several are selected.
    private void OnPropertiesClick(object? sender, RoutedEventArgs e) {
        if (DataContext is not ProcessesViewModel { SelectedRow: { } row } vm)
            return;

        var handle = TopLevel.GetTopLevel(this)?.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        vm.ShowProperties(handle, row.Pid);
    }

    // ----- View options popup -----
    //
    // The popup is not light-dismissed, so the rest of the window stays hoverable while it is open;
    // closing it on an outside press is done by hand, exactly as File Explorer's options flyout does.

    private void OnOptionsPopupOpened(object? sender, EventArgs e) =>
        TopLevel.GetTopLevel(this)?.AddHandler(
            InputElement.PointerPressedEvent, OnWindowPointerPressed,
            RoutingStrategies.Tunnel, handledEventsToo: true);

    private void OnOptionsPopupClosed(object? sender, EventArgs e) =>
        TopLevel.GetTopLevel(this)?.RemoveHandler(
            InputElement.PointerPressedEvent, OnWindowPointerPressed);

    private void OnWindowPointerPressed(object? sender, PointerPressedEventArgs e) {
        if (e.Source is not Visual source)
            return;

        // Leave the toggle to close itself (otherwise we'd close, then its click reopens), and ignore
        // presses inside the popup so its checkboxes stay clickable.
        if (OptionsButton.IsVisualAncestorOf(source))
            return;
        if (OptionsPopup.Child is Visual child && child.IsVisualAncestorOf(source))
            return;

        // Uncheck via the toggle so its state and the popup stay in sync; the press itself is left
        // unhandled so it still acts on whatever it landed on.
        OptionsButton.IsChecked = false;
    }

    // ----- Row context menu -----
    //
    // The menu is shared by every row, so its items act on the page's selection rather than on a row of
    // their own. OnListPressed above is what makes that safe: the right-click puts the row in the
    // selection before the menu opens.

    private void OnMenuEndTask(object? sender, RoutedEventArgs e) {
        if (DataContext is ProcessesViewModel vm)
            vm.RequestEndTaskCommand.Execute(null);
    }

    private void OnMenuProperties(object? sender, RoutedEventArgs e) => OnPropertiesClick(sender, e);

    private void OnMenuToggleExpand(object? sender, RoutedEventArgs e) {
        if (DataContext is ProcessesViewModel { SelectedRow: { } row } vm)
            vm.ToggleExpand(row);
    }

    private async void OnMenuCopyPid(object? sender, RoutedEventArgs e) {
        if (DataContext is not ProcessesViewModel { SelectedRow: { } row })
            return;
        if (TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
            return;

        try {
            await clipboard.SetTextAsync(row.PidText);
        } catch (Exception) {
            // Clipboard busy or denied by another app — the same silent give-up the Toolkit's copy makes.
        }
    }
}
