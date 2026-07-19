using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using System;
using System.Linq;

namespace DashDetective.Tabs.Processes;

public partial class ProcessesView : UserControl {
    public ProcessesView() {
        InitializeComponent();
    }

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
