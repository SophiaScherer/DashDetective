using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace DashDetective.Tabs.FileExplorer;

public partial class FileExplorerView : UserControl {
    public FileExplorerView() {
        InitializeComponent();
    }

    // The native Properties dialog needs the owning window handle, so it's invoked here rather
    // than from the view model (the same reason the Export dialog lives in MainWindow.axaml.cs).
    private void OnPropertiesClick(object? sender, RoutedEventArgs e) {
        if (DataContext is not FileExplorerViewModel { SelectedEntry: { } entry })
            return;

        var handle = TopLevel.GetTopLevel(this)?.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        ShellInterop.ShowProperties(handle, entry.FullPath);
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
