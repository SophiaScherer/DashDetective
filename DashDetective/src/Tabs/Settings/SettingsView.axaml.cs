using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using System;
using System.IO;

namespace DashDetective.Tabs.Settings;

public partial class SettingsView : UserControl {
    public SettingsView() {
        InitializeComponent();
    }

    /// <summary>Copies the diagnostics report to the clipboard (via the window's TopLevel).</summary>
    private async void OnCopyDiagnosticsClick(object? sender, RoutedEventArgs e) {
        if (DataContext is not SettingsViewModel vm)
            return;
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null)
            return;

        try {
            await clipboard.SetTextAsync(vm.BuildReport());
        } catch (Exception) {
            // Clipboard busy/denied — swallow so a failed copy can't take the app down.
        }
    }

    /// <summary>Exports the diagnostics report as a plain-text file (mirrors the toolbar Export).</summary>
    private async void OnExportReportClick(object? sender, RoutedEventArgs e) {
        if (DataContext is not SettingsViewModel vm)
            return;

        await SaveAsync(
            title: "Export system report",
            suggestedName: $"DashDetective-report-{DateTime.Now:yyyyMMdd-HHmmss}",
            extension: "txt",
            typeName: "Text report",
            pattern: "*.txt",
            content: vm.BuildReport);
    }

    /// <summary>Exports the rolling metric histories as a CSV file.</summary>
    private async void OnExportCsvClick(object? sender, RoutedEventArgs e) {
        if (DataContext is not SettingsViewModel vm)
            return;

        await SaveAsync(
            title: "Export metrics CSV",
            suggestedName: $"DashDetective-metrics-{DateTime.Now:yyyyMMdd-HHmmss}",
            extension: "csv",
            typeName: "CSV spreadsheet",
            pattern: "*.csv",
            content: vm.BuildMetricsCsv);
    }

    /// <summary>
    /// Shared save-file flow for the export buttons, following <c>MainWindow.OnExportClick</c>: pick a
    /// destination via the native dialog (needs the window's TopLevel), then write the built content.
    /// The content is built lazily only after a destination is chosen. Fully soft-failing.
    /// </summary>
    private async System.Threading.Tasks.Task SaveAsync(
        string title, string suggestedName, string extension, string typeName, string pattern,
        Func<string> content) {
        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage is null)
            return;

        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions {
            Title = title,
            SuggestedFileName = suggestedName,
            DefaultExtension = extension,
            FileTypeChoices = new[] {
                new FilePickerFileType(typeName) { Patterns = new[] { pattern } },
            },
        });

        if (file is null)
            return; // user cancelled

        try {
            await using var stream = await file.OpenWriteAsync();
            await using var writer = new StreamWriter(stream);
            await writer.WriteAsync(content());
        } catch (Exception) {
            // Disk full, permission denied, drive removed mid-write, etc. Swallow so a failed export
            // can't take the app down; the file simply isn't written.
        }
    }
}
