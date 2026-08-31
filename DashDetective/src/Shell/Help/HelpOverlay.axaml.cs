using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DashDetective.Shared;
using System;
using System.Linq;

namespace DashDetective.Shell.Help;

/// <summary>
/// The Help modal: a full-window scrim with a centred card. Embedded directly by the shell (like the
/// navigation bar) rather than routed through the <c>ViewLocator</c>, so it can sit above every other
/// surface including the nav bar.
///
/// Only the scrim-press dismissal and the reveal flash live here, because both are pure view concerns.
/// Esc is not handled here: the shell's shortcut dispatcher owns the key for the whole app and closes
/// this modal ahead of anything else, so there is one place that decides what Esc means.
/// </summary>
public partial class HelpOverlay : UserControl {
    private HelpViewModel? _boundViewModel;

    public HelpOverlay() => InitializeComponent();

    protected override void OnDataContextChanged(EventArgs e) {
        base.OnDataContextChanged(e);

        if (_boundViewModel is not null)
            _boundViewModel.RevealRequested -= OnRevealRequested;

        _boundViewModel = DataContext as HelpViewModel;

        if (_boundViewModel is not null)
            _boundViewModel.RevealRequested += OnRevealRequested;
    }

    // A press anywhere on the scrim closes, except one that landed inside the card — otherwise
    // interacting with the modal's own content would dismiss it.
    private void OnScrimPointerPressed(object? sender, PointerPressedEventArgs e) {
        if (e.Source is Visual source && Card.IsVisualAncestorOf(source))
            return;

        (DataContext as HelpViewModel)?.Close();
    }

    /// <summary>
    /// Scrolls a topic into view and flashes it. Rows are found by the key in their <c>Tag</c> rather
    /// than by name, so a topic added to the content table becomes reachable without touching this file.
    ///
    /// Posted because the reveal arrives in the same breath as the open and the tab switch that made the
    /// row's section visible: it is not in the visual tree until that layout pass has run.
    /// </summary>
    private void OnRevealRequested(string topicKey) =>
        Dispatcher.UIThread.Post(() => {
            if (FindRow(topicKey) is not { } row)
                return;

            row.BringIntoView();
            RevealFlash.Flash(row);
        }, DispatcherPriority.Loaded);

    private Border? FindRow(string topicKey) =>
        this.GetVisualDescendants()
            .OfType<Border>()
            .FirstOrDefault(border => border.Tag as string == topicKey);
}
