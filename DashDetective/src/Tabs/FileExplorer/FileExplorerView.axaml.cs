using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System;
using System.Linq;

namespace DashDetective.Tabs.FileExplorer;

public partial class FileExplorerView : UserControl {
    private FileExplorerViewModel? _boundViewModel;

    public FileExplorerView() {
        InitializeComponent();

        // The Options popup is deliberately overlay-free (IsLightDismissEnabled=False) so the rest
        // of the window stays hoverable while it's open. We re-add just the "close on outside click"
        // half of light dismiss ourselves: a top-level pointer-press listener, active only while the
        // popup is open, that closes it unless the press landed on the toggle or inside the popup.
        OptionsPopup.Opened += OnOptionsPopupOpened;
        OptionsPopup.Closed += OnOptionsPopupClosed;
    }

    // The list's own width decides which columns still fit. Reported from the view because there is
    // no converter-free path from an element's size to a view model property. Fires on splitter drags
    // as well as window resizes, which is exactly what's wanted here.
    private void OnTableSizeChanged(object? sender, SizeChangedEventArgs e) =>
        _boundViewModel?.SetTableWidth(e.NewSize.Width);

    // Remembered so a splitter-dragged width survives a collapse and restore.
    private GridLength _treeColumnWidth = new(FileExplorerPanes.TreeWidth);
    private GridLength _detailsColumnWidth = new(FileExplorerPanes.DetailsWidth);
    private bool _treeShown = true;
    private bool _detailsShown = true;

    // The side panes collapse rather than clip once the page can no longer hold their minimums. Done
    // here because it is pure view geometry: hiding a pane is not enough, its column has to be zeroed
    // or it keeps reserving width and the grid still overflows.
    private void OnPaneAreaSizeChanged(object? sender, SizeChangedEventArgs e) {
        // Columns are reached by index off the sender: a ColumnDefinition is not a control, so x:Name
        // on one generates no field. Order is tree, splitter, list, splitter, details.
        if (sender is not Grid grid || grid.ColumnDefinitions.Count < 5)
            return;

        ApplyPane(FileExplorerPanes.ShowTree(e.NewSize.Width), TreePane, TreeSplitter,
                  grid.ColumnDefinitions[0], grid.ColumnDefinitions[1],
                  ref _treeShown, ref _treeColumnWidth, 180);
        ApplyPane(FileExplorerPanes.ShowDetails(e.NewSize.Width), DetailsPane, DetailsSplitter,
                  grid.ColumnDefinitions[4], grid.ColumnDefinitions[3],
                  ref _detailsShown, ref _detailsColumnWidth, 220);
    }

    private static void ApplyPane(bool show, Control pane, Control splitter,
                                  ColumnDefinition column, ColumnDefinition splitterColumn,
                                  ref bool shown, ref GridLength remembered, double minWidth) {
        if (show == shown)
            return;
        shown = show;

        if (show) {
            column.MinWidth = minWidth;
            column.Width = remembered;
            splitterColumn.Width = GridLength.Auto;
        } else {
            remembered = column.Width;
            column.MinWidth = 0;
            column.Width = new GridLength(0);
            splitterColumn.Width = new GridLength(0);
        }

        pane.IsVisible = show;
        splitter.IsVisible = show;
    }

    // Scroll-to-top on folder navigation is driven from the view model (it knows when the path
    // actually changes); the view owns the ScrollViewer, so it listens for the request here.
    protected override void OnDataContextChanged(EventArgs e) {
        base.OnDataContextChanged(e);

        if (_boundViewModel is not null) {
            _boundViewModel.ScrollToTopRequested -= OnScrollToTopRequested;
            _boundViewModel.PathEditRequested -= OnPathEditRequested;
        }

        _boundViewModel = DataContext as FileExplorerViewModel;

        if (_boundViewModel is not null) {
            _boundViewModel.ScrollToTopRequested += OnScrollToTopRequested;
            _boundViewModel.PathEditRequested += OnPathEditRequested;
        }
    }

    private void OnScrollToTopRequested() => FileListScroll.ScrollToHome();

    // Selecting the whole path means typing replaces it, while Home/End still edit in place — the
    // behaviour of every address bar.
    private void OnPathEditRequested() => PathBox.FocusAndSelectAll();

    // Clicking away abandons the edit, matching Esc. Committing hides the box first, so the cancel
    // that follows finds the edit already closed and does nothing.
    private void OnPathBoxLostFocus(object? sender, RoutedEventArgs e) =>
        (DataContext as FileExplorerViewModel)?.CancelPathEditCommand.Execute(null);

    // Clicking the breadcrumb field starts editing the path, the same gesture Windows Explorer has.
    // A press on a crumb is left alone so it still navigates, and a press inside the box while already
    // editing must not restart the edit and re-select everything under the caret.
    private void OnBreadcrumbPointerPressed(object? sender, PointerPressedEventArgs e) {
        if (DataContext is not FileExplorerViewModel { IsPathEditing: false } vm)
            return;

        if (e.Source is Visual source &&
            source.GetSelfAndVisualAncestors().OfType<Button>().Any())
            return;

        vm.BeginPathEditCommand.Execute(null);
    }

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

        // Leave the toggle to close itself (otherwise we'd close, then its click reopens), and
        // ignore presses inside the popup so its checkboxes stay clickable in overlay-popup mode.
        if (OptionsButton.IsVisualAncestorOf(source))
            return;
        if (OptionsPopup.Child is Visual child && child.IsVisualAncestorOf(source))
            return;

        // Uncheck via the toggle so its state and the popup stay in sync; the press itself is left
        // unhandled so it still acts on whatever it landed on (the pass-through we wanted).
        OptionsButton.IsChecked = false;
    }

    // The native Properties dialog needs the owning window handle, so it's invoked here rather
    // than from the view model (the same reason the Export dialog lives in MainWindow.axaml.cs).
    private void OnPropertiesClick(object? sender, RoutedEventArgs e) {
        if (DataContext is not FileExplorerViewModel { SelectedEntry: { } entry } vm)
            return;

        var handle = TopLevel.GetTopLevel(this)?.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        vm.ShowProperties(handle, entry.FullPath);
    }

    // Single tap selects the row (drives the details pane); double tap activates it (a folder
    // navigates into itself, a file opens in Phase 5). Row events are handled here rather than in
    // the view model because double-tap has no XAML command binding.
    private void OnEntryTapped(object? sender, TappedEventArgs e) {
        if (sender is Control { DataContext: FileEntry entry } && DataContext is FileExplorerViewModel vm)
            vm.SelectEntry(entry);
    }

    private void OnEntryDoubleTapped(object? sender, TappedEventArgs e) {
        if (sender is Control { DataContext: FileEntry entry } && DataContext is FileExplorerViewModel vm)
            vm.ActivateEntry(entry);
    }
}
