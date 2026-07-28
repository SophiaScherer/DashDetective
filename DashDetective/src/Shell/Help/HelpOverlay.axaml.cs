using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace DashDetective.Shell.Help;

/// <summary>
/// The Help modal: a full-window scrim with a centred card. Embedded directly by the shell (like the
/// navigation bar) rather than routed through the <c>ViewLocator</c>, so it can sit above every other
/// surface including the nav bar.
///
/// Only the scrim-press dismissal lives here, because it is a pure pointer concern. Esc is not handled
/// here: the shell's shortcut dispatcher owns the key for the whole app and closes this modal ahead of
/// anything else, so there is one place that decides what Esc means.
/// </summary>
public partial class HelpOverlay : UserControl {
    public HelpOverlay() => InitializeComponent();

    // A press anywhere on the scrim closes, except one that landed inside the card — otherwise
    // interacting with the modal's own content would dismiss it.
    private void OnScrimPointerPressed(object? sender, PointerPressedEventArgs e) {
        if (e.Source is Visual source && Card.IsVisualAncestorOf(source))
            return;

        (DataContext as HelpViewModel)?.Close();
    }
}
