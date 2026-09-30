using DashDetective.Shared.Shortcuts;
using DashDetective.Shell.Help;
using DashDetective.Tabs.Settings;
using System;

namespace DashDetective.Shell;

/// <summary>
/// The modal step of the shell's shortcut chain: what a key does while Help or the accent picker is open.
///
/// Split out of <see cref="MainWindowViewModel"/>, like <see cref="RefreshHint"/>, so it is testable without
/// constructing the shell, which builds every page and its samplers.
/// </summary>
internal static class ModalShortcuts {
    /// <summary>The scope keys resolve in: Global while a modal is open, so no page's bindings apply
    /// behind the scrim; otherwise <paramref name="underneath"/>.</summary>
    public static ShortcutScope Scope(HelpViewModel help, AccentPickerViewModel picker, ShortcutScope underneath) =>
        help.IsOpen || picker.IsOpen ? ShortcutScope.Global : underneath;

    /// <summary>Runs a shortcut against the open modal. Null when none is open, so the chain goes on;
    /// otherwise whether the key was consumed.</summary>
    public static bool? Handle(ShortcutId id, HelpViewModel help, AccentPickerViewModel picker) {
        if (help.IsOpen)
            return Route(id, help.Close);

        if (picker.IsOpen)
            return Route(id, () => picker.CancelCommand.Execute(null));

        return null;
    }

    /// <summary>Esc dismisses; Enter falls through so a focused button in the card presses; everything
    /// else is swallowed, so nothing acts on the page behind the scrim.</summary>
    private static bool Route(ShortcutId id, Action dismiss) {
        if (id == ShortcutId.Activate)
            return false;

        if (id == ShortcutId.Escape)
            dismiss();

        return true;
    }
}
