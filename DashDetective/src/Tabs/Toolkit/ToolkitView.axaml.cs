using Avalonia;
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
using System.Collections.Generic;
using System.Linq;

namespace DashDetective.Tabs.Toolkit;

public partial class ToolkitView : UserControl {
    /// <summary>How long a copied row's glyph stays accented before fading back.</summary>
    private static readonly TimeSpan CopiedDuration = TimeSpan.FromSeconds(1);

    private ToolkitViewModel? _boundViewModel;

    public ToolkitView() {
        InitializeComponent();

        // Tunnel, so a repeat press is refused before the play button's own handler turns it into a click.
        AddHandler(PointerPressedEvent, OnPlayPressed, RoutingStrategies.Tunnel);
    }

    /// <summary>What holds keyboard focus right now, window-wide — read when Enter is pressed rather than
    /// tracked, so focus on a toolbar or nav control is seen too.</summary>
    private (ToolkitActivation.Focus, ToolkitEntry?) ProbeFocus() {
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
        if (focused is null or TopLevel)
            return (ToolkitActivation.Focus.Nothing, null);

        if (focused is Button { DataContext: ToolkitEntry row } button && button.Classes.Contains("rowRun") &&
            button.GetVisualAncestors().Contains(this))
            return (ToolkitActivation.Focus.RowBody, row);

        return (ToolkitActivation.Focus.Other, null);
    }

    // The second click of a double click on play is swallowed, so a launch that finished in between is
    // not started again.
    private void OnPlayPressed(object? sender, PointerPressedEventArgs e) {
        if (ToolkitActivation.IsRepeatPress(e.ClickCount) && e.Source is Visual source &&
            source.GetSelfAndVisualAncestors().OfType<Button>().FirstOrDefault() is { } button &&
            button.Classes.Contains("play"))
            e.Handled = true;
    }

    // Selection follows keyboard focus, so Enter on a row reached by Tab runs that row and not one
    // clicked earlier. A pointer press is left to the click, which selects anyway.
    private void OnRowGotFocus(object? sender, FocusChangedEventArgs e) {
        if (e.NavigationMethod is NavigationMethod.Tab or NavigationMethod.Directional &&
            sender is Control { DataContext: ToolkitEntry entry } && _boundViewModel is { } vm)
            vm.SelectCommand.Execute(entry);
    }

    // Double-tap has no XAML command binding, so it is routed here — the File Explorer rows' shape. The
    // two clicks inside it only select, so this is the gesture's one effect.
    private void OnRowDoubleTapped(object? sender, TappedEventArgs e) {
        if (sender is not Control { DataContext: ToolkitEntry entry } || _boundViewModel is not { } vm)
            return;

        _ = vm.Activate(entry);
        e.Handled = true;
    }

    protected override void OnDataContextChanged(EventArgs e) {
        base.OnDataContextChanged(e);

        if (_boundViewModel is not null) {
            _boundViewModel.SearchFocusRequested -= FocusSearch;
            _boundViewModel.RevealRequested -= ScheduleReveal;
            _boundViewModel.FocusProbe = null;
        }

        _boundViewModel = DataContext as ToolkitViewModel;

        if (_boundViewModel is not null) {
            _boundViewModel.SearchFocusRequested += FocusSearch;
            _boundViewModel.RevealRequested += ScheduleReveal;
            _boundViewModel.FocusProbe = ProbeFocus;

            // A jump from universal search reaches the view model before this view exists, so the
            // reveal it left waiting is collected here rather than only on the event.
            ScheduleReveal();
        }
    }

    // Focusing selects what's already typed, so a second "/" replaces the term rather than appending
    // to it — as the Processes filter does.
    private void FocusSearch() => SearchBox.FocusAndSelectAll();

    /// <summary>
    /// Scrolls the row waiting to be revealed into view and flashes it, if there is one. Rows are found
    /// by the entry in their <c>Tag</c> — by identity, so a custom row sharing a catalog row's label is
    /// not flashed in its place.
    ///
    /// **Every** row carrying the entry is flashed, not just the first. A custom command the user filed
    /// under a category owns two rows — one under My Commands, one under that category — and flashing
    /// only one of them would leave the other looking like a different command that happens to share a
    /// name. The scroll goes to the first, since only one can be brought into view.
    ///
    /// Posted at Loaded because resetting the filter rebuilds the rows: the rows do not exist in the
    /// visual tree until that layout pass has run.
    /// </summary>
    private void ScheduleReveal() {
        if (_boundViewModel?.TakePendingReveal() is not { } entry)
            return;

        Dispatcher.UIThread.Post(() => {
            var rows = FindRows(entry);
            if (rows.Count == 0)
                return;

            rows[0].BringIntoView();
            foreach (var row in rows)
                RevealFlash.Flash(row);
        }, DispatcherPriority.Loaded);
    }

    private List<Border> FindRows(ToolkitEntry entry) =>
        [.. this.GetVisualDescendants()
               .OfType<Border>()
               .Where(border => ReferenceEquals(border.Tag, entry))];

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

    /// <summary>Saves the Execution Log through the shared save dialog, offering the same formats as the
    /// system report.</summary>
    private async void OnExportLogClick(object? sender, RoutedEventArgs e) {
        if (DataContext is not ToolkitViewModel vm)
            return;

        var path = await FileSave.SaveAsync(
            this,
            title: "Export execution log",
            suggestedName: $"DashDetective-utilities-log-{DateTime.Now:yyyyMMdd-HHmmss}",
            formats: DiagnosticsFormats.Offered,
            content: vm.BuildLog);

        if (path is not null)
            vm.Notify?.Invoke(Notices.Exported(path));
    }
}
