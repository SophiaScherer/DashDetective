using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace DashDetective.Tabs.FileExplorer;

public partial class FileExplorerView : UserControl {
    public FileExplorerView() {
        InitializeComponent();

        // The Options popup is deliberately overlay-free (IsLightDismissEnabled=False) so the rest
        // of the window stays hoverable while it's open. We re-add just the "close on outside click"
        // half of light dismiss ourselves: a top-level pointer-press listener, active only while the
        // popup is open, that closes it unless the press landed on the toggle or inside the popup.
        OptionsPopup.Opened += OnOptionsPopupOpened;
        OptionsPopup.Closed += OnOptionsPopupClosed;
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
