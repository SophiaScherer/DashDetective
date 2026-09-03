using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DashDetective.Services.Diagnostics;
using DashDetective.Services.Notifications;
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

    /// <summary>A capture box armed or stood down. Held on the view model because the shell's key
    /// listener tunnels from the window and would otherwise run the shortcut being rebound.</summary>
    private void OnShortcutCapturingChanged(object? sender, bool capturing) =>
        (DataContext as SettingsViewModel)?.SetCapturing(capturing);

    /// <summary>A capture box produced a gesture. The row it belongs to is the box's own DataContext —
    /// the template has no other way to say which shortcut was being rebound.</summary>
    private void OnShortcutCaptured(object? sender, KeyGesture gesture) {
        if (DataContext is SettingsViewModel vm && (sender as Control)?.DataContext is ShortcutRow row)
            vm.Rebind(row, gesture);
    }

    private void OnShortcutResetRequested(object? sender, EventArgs e) {
        if (DataContext is SettingsViewModel vm && (sender as Control)?.DataContext is ShortcutRow row)
            vm.ResetShortcut(row);
    }

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
            // Clipboard busy/denied — swallow so a failed copy can't take the app down, and say nothing:
            // a confirmation for a copy that did not happen is worse than none.
            return;
        }

        vm.Notify?.Invoke(Notices.DiagnosticsCopied);
    }

    /// <summary>Exports the system report, in whichever format the chosen filename asks for (mirrors the
    /// toolbar Export).</summary>
    private async void OnExportReportClick(object? sender, RoutedEventArgs e) {
        if (DataContext is not SettingsViewModel vm)
            return;

        var path = await FileSave.SaveAsync(
            this,
            title: "Export system report",
            suggestedName: $"DashDetective-report-{DateTime.Now:yyyyMMdd-HHmmss}",
            formats: DiagnosticsFormats.Offered,
            content: vm.BuildReport);

        if (path is not null)
            vm.Notify?.Invoke(Notices.Exported(path));
    }

    /// <summary>Exports the rolling metric histories as a CSV file. A different artifact from the report
    /// above — the 60-sample history rather than a snapshot — so it keeps its own button and its one
    /// format.</summary>
    private async void OnExportCsvClick(object? sender, RoutedEventArgs e) {
        if (DataContext is not SettingsViewModel vm)
            return;

        var path = await FileSave.SaveAsync(
            this,
            title: "Export metrics CSV",
            suggestedName: $"DashDetective-metrics-{DateTime.Now:yyyyMMdd-HHmmss}",
            formats: [DiagnosticsFormat.Csv],
            content: _ => vm.BuildMetricsCsv());

        if (path is not null)
            vm.Notify?.Invoke(Notices.Exported(path));
    }

}
