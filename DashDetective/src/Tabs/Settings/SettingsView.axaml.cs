using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DashDetective.Services.Diagnostics;
using DashDetective.Shared;
using System;
using System.Linq;

namespace DashDetective.Tabs.Settings;

public partial class SettingsView : UserControl {
    private SettingsViewModel? _boundViewModel;

    public SettingsView() {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e) {
        base.OnDataContextChanged(e);

        if (_boundViewModel is not null)
            _boundViewModel.RevealRequested -= OnRevealRequested;

        _boundViewModel = DataContext as SettingsViewModel;

        if (_boundViewModel is not null)
            _boundViewModel.RevealRequested += OnRevealRequested;
    }

    /// <summary>
    /// Scrolls a setting into view and flashes it. Rows are found by the <c>SettingId</c> in their
    /// <c>Tag</c> rather than by name, so a row added to the page becomes reachable by adding one
    /// attribute — there is no switch here to keep in step.
    ///
    /// Posted because the reveal arrives in the same breath as the navigation that made this page
    /// current: the rows do not exist in the visual tree until that layout pass has run.
    /// </summary>
    private void OnRevealRequested(SettingId id) =>
        Dispatcher.UIThread.Post(() => {
            if (FindRow(id) is not { } row)
                return;

            row.BringIntoView();
            RevealFlash.Flash(row);
        }, DispatcherPriority.Loaded);

    private Border? FindRow(SettingId id) =>
        this.GetVisualDescendants()
            .OfType<Border>()
            .FirstOrDefault(border => border.Tag is SettingId tag && tag == id);

    /// <summary>Copies the diagnostics report to the clipboard (via the window's TopLevel).</summary>
    private async void OnCopyDiagnosticsClick(object? sender, RoutedEventArgs e) {
        if (DataContext is not SettingsViewModel vm)
            return;
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null)
            return;

        try {
            await clipboard.SetTextAsync(vm.BuildReport(DiagnosticsFormat.Text));
        } catch (Exception) {
            // Clipboard busy/denied — swallow so a failed copy can't take the app down.
        }
    }

    /// <summary>Exports the system report, in whichever format the chosen filename asks for (mirrors the
    /// toolbar Export).</summary>
    private async void OnExportReportClick(object? sender, RoutedEventArgs e) {
        if (DataContext is not SettingsViewModel vm)
            return;

        await FileSave.SaveAsync(
            this,
            title: "Export system report",
            suggestedName: $"DashDetective-report-{DateTime.Now:yyyyMMdd-HHmmss}",
            formats: DiagnosticsFormats.Offered,
            content: vm.BuildReport);
    }

    /// <summary>Exports the rolling metric histories as a CSV file. A different artifact from the report
    /// above — the 60-sample history rather than a snapshot — so it keeps its own button and its one
    /// format.</summary>
    private async void OnExportCsvClick(object? sender, RoutedEventArgs e) {
        if (DataContext is not SettingsViewModel vm)
            return;

        await FileSave.SaveAsync(
            this,
            title: "Export metrics CSV",
            suggestedName: $"DashDetective-metrics-{DateTime.Now:yyyyMMdd-HHmmss}",
            formats: [DiagnosticsFormat.Csv],
            content: _ => vm.BuildMetricsCsv());
    }

}
