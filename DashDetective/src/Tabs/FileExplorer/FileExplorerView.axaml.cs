using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace DashDetective.Tabs.FileExplorer;

public partial class FileExplorerView : UserControl {
    public FileExplorerView() {
        InitializeComponent();
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
