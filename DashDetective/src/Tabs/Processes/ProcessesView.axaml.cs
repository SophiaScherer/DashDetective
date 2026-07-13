using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace DashDetective.Tabs.Processes;

public partial class ProcessesView : UserControl {
    public ProcessesView() {
        InitializeComponent();
    }

    // Tap selects the row (drives the highlight + End task / Properties enablement). Handled here
    // rather than in the view model because a row tap has no XAML command binding — the same pattern
    // as File Explorer's row selection.
    private void OnRowTapped(object? sender, TappedEventArgs e) {
        if (sender is Control { DataContext: ProcessRow row } && DataContext is ProcessesViewModel vm)
            vm.SelectRow(row);
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
