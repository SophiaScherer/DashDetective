using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
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
    }

    // The table's own width decides which columns still fit. Reported from the view because there is
    // no converter-free path from an element's size to a view model property.
    private void OnTableSizeChanged(object? sender, SizeChangedEventArgs e) =>
        _viewModel?.SetTableWidth(e.NewSize.Width);

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
        if (DataContext is not ProcessesViewModel { SelectedRow: { } row })
            return;

        var handle = TopLevel.GetTopLevel(this)?.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        ProcessInterop.ShowProperties(handle, row.Pid);
    }
}
