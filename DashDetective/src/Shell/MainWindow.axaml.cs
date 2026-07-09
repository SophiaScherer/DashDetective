using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace DashDetective.Shell;

public partial class MainWindow : Window {
    public MainWindow() {
        InitializeComponent();
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
