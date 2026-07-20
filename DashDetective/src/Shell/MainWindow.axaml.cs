using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using System;
using System.ComponentModel;
using System.IO;

namespace DashDetective.Shell;

public partial class MainWindow : Window {
    // Set by the tray "Exit" so a subsequent close actually exits instead of hiding to tray.
    private bool _exitRequested;

    public MainWindow() {
        InitializeComponent();
        Closing += OnClosing;
    }

    /// <summary>
    /// Close handler: when the "Show in system tray" setting is on, closing the window hides it to the
    /// tray (the app keeps running) rather than exiting. A close driven by the tray "Exit" item, or a
    /// close while the setting is off, proceeds normally — the last window closing shuts the app down,
    /// which runs the composition root's disposal (flushing settings, releasing timers/PDH handles).
    /// </summary>
    private void OnClosing(object? sender, WindowClosingEventArgs e) {
        if (!_exitRequested && DataContext is MainWindowViewModel { ShowInTray: true }) {
            e.Cancel = true;
            Hide();
        }
    }

    /// <summary>Restores and focuses the window from the tray.</summary>
    public void ShowFromTray() {
        Show();
        Activate();
    }

    /// <summary>Really exits from the tray: closes the window (bypassing hide-to-tray).</summary>
    public void ExitFromTray() {
        _exitRequested = true;
        Close();
    }

    /// <summary>
    /// Exports the current system snapshot as a plain-text report. Owns the file-save dialog here
    /// (rather than in the view model) because the picker needs the window's <see cref="TopLevel"/>.
    /// </summary>
    private async void OnExportClick(object? sender, RoutedEventArgs e) {
        if (DataContext is not MainWindowViewModel vm)
            return;

        var storage = StorageProvider;
        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions {
            Title = "Export system report",
            SuggestedFileName = $"DashDetective-report-{DateTime.Now:yyyyMMdd-HHmmss}",
            DefaultExtension = "txt",
            FileTypeChoices = new[] {
                new FilePickerFileType("Text report") { Patterns = new[] { "*.txt" } },
            },
        });

        if (file is null)
            return; // user cancelled

        try {
            await using var stream = await file.OpenWriteAsync();
            await using var writer = new StreamWriter(stream);
            await writer.WriteAsync(vm.BuildReport());
        } catch (Exception) {
            // Disk full, permission denied, drive removed mid-write, etc. Swallow so a failed
            // export can't take the app down; the file simply isn't written.
        }
    }
}
