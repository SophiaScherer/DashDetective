using Avalonia.Input;
using System.Collections.Generic;

namespace DashDetective.Shared.Shortcuts;

/// <summary>
/// The single source of truth for every keyboard shortcut: gestures, focus guards and Help copy. The
/// shell resolves keys through <see cref="TryResolve"/> and the Help modal renders <see cref="All"/>,
/// so a live binding can never go undocumented (or a documented one go dead). Held as a static table
/// like <c>HelpContent</c> and <c>HardwareCatalog</c>, and free of any control types, so it is
/// testable without a render backend.
/// </summary>
public static class ShortcutCatalog {
    /// <summary>Every shortcut, in the order Help lists them.</summary>
    public static IReadOnlyList<Shortcut> All { get; } = [
        // ----- Global navigation -----
        // The eight tab jumps share one Help row; the numpad digits are bound too so the shortcut
        // works the same either side of the keyboard.
        Tab(ShortcutId.NavigateTab1, Key.D1, Key.NumPad1,
            keys: "Ctrl+1 … Ctrl+8", description: "Jump to a tab by position (Dashboard → Settings)"),
        Tab(ShortcutId.NavigateTab2, Key.D2, Key.NumPad2),
        Tab(ShortcutId.NavigateTab3, Key.D3, Key.NumPad3),
        Tab(ShortcutId.NavigateTab4, Key.D4, Key.NumPad4),
        Tab(ShortcutId.NavigateTab5, Key.D5, Key.NumPad5),
        Tab(ShortcutId.NavigateTab6, Key.D6, Key.NumPad6),
        Tab(ShortcutId.NavigateTab7, Key.D7, Key.NumPad7),
        Tab(ShortcutId.NavigateTab8, Key.D8, Key.NumPad8),

        new(ShortcutId.NextTab, [new KeyGesture(Key.Tab, KeyModifiers.Control)],
            "Ctrl+Tab", "Go to the next tab", ShortcutScope.Global),

        new(ShortcutId.PreviousTab, [new KeyGesture(Key.Tab, KeyModifiers.Control | KeyModifiers.Shift)],
            "Ctrl+Shift+Tab", "Go to the previous tab", ShortcutScope.Global),

        new(ShortcutId.ToggleNavCollapse, [new KeyGesture(Key.B, KeyModifiers.Control)],
            "Ctrl+B", "Collapse or expand the navigation bar", ShortcutScope.Global),

        new(ShortcutId.OpenSettings, [new KeyGesture(Key.OemComma, KeyModifiers.Control)],
            "Ctrl+,", "Open Settings", ShortcutScope.Global),

        // ----- Toolbar actions -----
        new(ShortcutId.ToggleLive, [new KeyGesture(Key.P, KeyModifiers.Control)],
            "Ctrl+P", "Pause or resume live sampling", ShortcutScope.Global),

        new(ShortcutId.Refresh, [new KeyGesture(Key.F5), new KeyGesture(Key.R, KeyModifiers.Control)],
            "F5 / Ctrl+R", "Refresh the current page", ShortcutScope.Global),

        new(ShortcutId.Export, [new KeyGesture(Key.E, KeyModifiers.Control)],
            "Ctrl+E", "Export a system report", ShortcutScope.Global),

        // Ctrl+/ sits on OemQuestion, so it is layout-dependent; F1 is the binding that always works.
        new(ShortcutId.ShowHelp, [new KeyGesture(Key.F1), new KeyGesture(Key.OemQuestion, KeyModifiers.Control)],
            "F1 / Ctrl+/", "Open this Help window", ShortcutScope.Global),

        new(ShortcutId.Escape, [new KeyGesture(Key.Escape)],
            "Esc", "Close Help, cancel an open dialog, or dismiss the alert banner", ShortcutScope.Global),
    ];

    /// <summary>Builds one of the Ctrl+digit tab jumps. Only the first carries Help copy — one row
    /// covers the run (see <see cref="Shortcut.ShowInHelp"/>).</summary>
    private static Shortcut Tab(ShortcutId id, Key digit, Key numpad, string keys = "", string description = "") =>
        new(id,
            [new KeyGesture(digit, KeyModifiers.Control), new KeyGesture(numpad, KeyModifiers.Control)],
            keys, description, ShortcutScope.Global, ShowInHelp: keys.Length > 0);

    /// <summary>
    /// Resolves a key press to the action it triggers. <paramref name="textInputFocused"/> suppresses
    /// every shortcut that isn't safe to steal from a text box, so typing "/" into a search field
    /// never fires an app action.
    /// </summary>
    public static bool TryResolve(Key key, KeyModifiers modifiers, bool textInputFocused, out ShortcutId id) {
        foreach (var shortcut in All) {
            if (textInputFocused && !shortcut.AllowInTextInput)
                continue;

            foreach (var gesture in shortcut.Gestures) {
                // Modifiers must match exactly, so Ctrl+Shift+Tab can mean something other than Ctrl+Tab.
                if (gesture.Key != key || gesture.KeyModifiers != modifiers)
                    continue;

                id = shortcut.Id;
                return true;
            }
        }

        id = default;
        return false;
    }
}
