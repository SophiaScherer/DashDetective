using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DashDetective.Services.Diagnostics;
using DashDetective.Services.Notifications;
using DashDetective.Shared;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DashDetective.Tabs.Toolkit;

public partial class ToolkitView : UserControl {
    /// <summary>How long a copied row's glyph stays accented before fading back.</summary>
    private static readonly TimeSpan CopiedDuration = TimeSpan.FromSeconds(1);

    private ToolkitViewModel? _boundViewModel;

    public ToolkitView() {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e) {
        base.OnDataContextChanged(e);

        if (_boundViewModel is not null) {
            _boundViewModel.SearchFocusRequested -= FocusSearch;
            _boundViewModel.RevealRequested -= ScheduleReveal;
        }

        _boundViewModel = DataContext as ToolkitViewModel;

        if (_boundViewModel is not null) {
            _boundViewModel.SearchFocusRequested += FocusSearch;
            _boundViewModel.RevealRequested += ScheduleReveal;

            // A jump from universal search reaches the view model before this view exists, so the
            // reveal it left waiting is collected here rather than only on the event.
            ScheduleReveal();
        }
    }

    // Focusing selects what's already typed, so a second "/" replaces the term rather than appending
    // to it — as the Processes filter does.
    private void FocusSearch() => SearchBox.FocusAndSelectAll();

    /// <summary>
    /// Scrolls the command waiting to be revealed into view and flashes it, if there is one. Rows are
    /// found by the command in their <c>Tag</c>, so nothing here has to know the command set — the same
    /// seam SettingsView uses.
    ///
    /// **Every** row carrying the command is flashed, not just the first. A custom command the user filed
    /// under a category owns two rows — one under My Commands, one under that category — and flashing
    /// only one of them would leave the other looking like a different command that happens to share a
    /// name. The scroll goes to the first, since only one can be brought into view.
    ///
    /// Posted at Loaded because resetting the filter rebuilds the rows: the rows do not exist in the
    /// visual tree until that layout pass has run.
    /// </summary>
    private void ScheduleReveal() {
        if (_boundViewModel?.TakePendingReveal() is not { } command)
            return;

        Dispatcher.UIThread.Post(() => {
            var rows = FindRows(command);
            if (rows.Count == 0)
                return;

            rows[0].BringIntoView();
            foreach (var row in rows)
                RevealFlash.Flash(row);
        }, DispatcherPriority.Loaded);
    }

    private List<Border> FindRows(string command) =>
        [.. this.GetVisualDescendants()
               .OfType<Border>()
               .Where(border => border.Tag is string tag && tag == command)];

    /// <summary>
    /// Copies the row's command to the clipboard. Lives here rather than in the view model because the
    /// clipboard is reached through the window's <c>TopLevel</c> — the same reason
    /// <c>SettingsView.OnCopyDiagnosticsClick</c> does. Soft-failing: a refused copy must not take the
    /// app down.
    /// </summary>
    private async void OnCopyClick(object? sender, RoutedEventArgs e) {
        if (sender is not Button button ||
            button.DataContext is not ToolkitEntry entry ||
            DataContext is not ToolkitViewModel vm)
            return;

        if (TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
            return;

        try {
            await clipboard.SetTextAsync(vm.CopyTextFor(entry));
        } catch (Exception) {
            // Clipboard busy or denied by another app — say nothing, but don't confirm either.
            return;
        }

        ConfirmCopied(button);
    }

    // Accent the glyph, then let the style's transition fade it back — the click needs an answer, and
    // the log is for what ran, not for what was copied.
    private static void ConfirmCopied(Button button) {
        button.Classes.Remove("copied");
        button.Classes.Add("copied");
        DispatcherTimer.RunOnce(() => button.Classes.Remove("copied"), CopiedDuration);
    }

    /// <summary>Saves the Execution Log through the shared save dialog. A transcript is text and nothing
    /// else, so unlike the system report it offers the one format.</summary>
    private async void OnExportLogClick(object? sender, RoutedEventArgs e) {
        if (DataContext is not ToolkitViewModel vm)
            return;

        var path = await FileSave.SaveAsync(
            this,
            title: "Export execution log",
            suggestedName: $"DashDetective-toolkit-log-{DateTime.Now:yyyyMMdd-HHmmss}",
            formats: [DiagnosticsFormat.Text],
            content: _ => vm.BuildLogText());

        if (path is not null)
            vm.Notify?.Invoke(Notices.Exported(path));
    }
}
