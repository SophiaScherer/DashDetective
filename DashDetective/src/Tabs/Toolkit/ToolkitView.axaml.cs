using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System;
using System.Linq;

namespace DashDetective.Tabs.Toolkit;

public partial class ToolkitView : UserControl {
    /// <summary>How long a revealed row stays tinted before fading back (the fade itself is the
    /// <c>cmdRow</c> style's brush transition).</summary>
    private static readonly TimeSpan HighlightDuration = TimeSpan.FromSeconds(1.6);

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
    private void FocusSearch() {
        SearchBox.Focus();
        SearchBox.SelectAll();
    }

    /// <summary>
    /// Scrolls the command waiting to be revealed into view and flashes it, if there is one. Rows are
    /// found by the command in their <c>Tag</c>, so nothing here has to know the command set — the same
    /// seam SettingsView uses.
    ///
    /// Posted at Loaded because resetting the filter rebuilds the rows: the row does not exist in the
    /// visual tree until that layout pass has run.
    /// </summary>
    private void ScheduleReveal() {
        if (_boundViewModel?.TakePendingReveal() is not { } command)
            return;

        Dispatcher.UIThread.Post(() => {
            if (FindRow(command) is not { } row)
                return;

            row.BringIntoView();
            Flash(row);
        }, DispatcherPriority.Loaded);
    }

    private Border? FindRow(string command) =>
        this.GetVisualDescendants()
            .OfType<Border>()
            .FirstOrDefault(border => border.Tag is string tag && tag == command);

    // Tint, then untint on a one-shot timer; the style's transition turns the untint into a fade.
    private static void Flash(Border row) {
        row.Classes.Remove("highlighted");
        row.Classes.Add("highlighted");
        DispatcherTimer.RunOnce(() => row.Classes.Remove("highlighted"), HighlightDuration);
    }

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
}
