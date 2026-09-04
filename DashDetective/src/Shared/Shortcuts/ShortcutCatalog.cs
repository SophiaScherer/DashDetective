using Avalonia.Input;
using System;
using System.Collections.Generic;

namespace DashDetective.Shared.Shortcuts;

/// <summary>
/// The <b>shipped default</b> for every keyboard shortcut: gestures, focus guards and Help copy. What is
/// actually in force is <see cref="ShortcutBindings"/> — these defaults with the user's rebinds applied —
/// and that is what the shell resolves through and Help renders, so a live binding can never go
/// undocumented (or a documented one go dead).
///
/// The resolution and grouping helpers here take the table as an argument so the bindings can run the
/// user's choices through this same code rather than through a second copy of it. Held as a static table
/// like <c>HelpContent</c> and <c>HardwareCatalog</c>, and free of any control types, so it is testable
/// without a render backend.
/// </summary>
public static class ShortcutCatalog {
    /// <summary>Every shortcut, in the order Help lists them.</summary>
    public static IReadOnlyList<Shortcut> All { get; } = [
        // ----- Global navigation -----
        // The nine tab jumps share one Help row; the numpad digits are bound too so the shortcut
        // works the same either side of the keyboard.
        Tab(ShortcutId.NavigateTab1, Key.D1, Key.NumPad1,
            keys: "Ctrl+1 … Ctrl+9", description: "Jump to a tab by position (Dashboard → Settings)"),
        Tab(ShortcutId.NavigateTab2, Key.D2, Key.NumPad2),
        Tab(ShortcutId.NavigateTab3, Key.D3, Key.NumPad3),
        Tab(ShortcutId.NavigateTab4, Key.D4, Key.NumPad4),
        Tab(ShortcutId.NavigateTab5, Key.D5, Key.NumPad5),
        Tab(ShortcutId.NavigateTab6, Key.D6, Key.NumPad6),
        Tab(ShortcutId.NavigateTab7, Key.D7, Key.NumPad7),
        Tab(ShortcutId.NavigateTab8, Key.D8, Key.NumPad8),
        Tab(ShortcutId.NavigateTab9, Key.D9, Key.NumPad9),

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

        // Ctrl+F belongs to universal search alone; a tab's own field is reached with "/" instead.
        new(ShortcutId.FocusSearch, [new KeyGesture(Key.F, KeyModifiers.Control)],
            "Ctrl+F", "Search pages, settings, processes and files", ShortcutScope.Global),

        // Tab is deliberately absent: accepting a ghosted completion is owned by the field showing one
        // (see GhostCompletionBox), because only the focused control knows whether there is a
        // suggestion to accept. Routing it through here would mean asking a view model about focus.

        new(ShortcutId.Escape, [new KeyGesture(Key.Escape)],
            "Esc", "Close Help, cancel an open dialog, or dismiss the alert banner", ShortcutScope.Global),

        // Enter must reach a text box the user is typing into, so it is bound only outside one.
        new(ShortcutId.Activate, [new KeyGesture(Key.Enter)],
            "Enter", "Open the selected item, or confirm an open dialog", ShortcutScope.Global,
            AllowInTextInput: false),

        new(ShortcutId.ToggleTheme, [new KeyGesture(Key.T, KeyModifiers.Control | KeyModifiers.Shift)],
            "Ctrl+Shift+T", "Switch between the light and dark theme", ShortcutScope.Global),

        // ----- Search results -----
        // The arrows are safe to steal from a text box only because this scope is live solely while the
        // search dropdown is open — everywhere else they still scroll the page (see ActiveScope).
        new(ShortcutId.SelectNextResult, [new KeyGesture(Key.Down)],
            "↓ / ↑", "Move through the search results", ShortcutScope.Search),

        new(ShortcutId.SelectPreviousResult, [new KeyGesture(Key.Up)],
            "", "", ShortcutScope.Search, ShowInHelp: false),

        // ----- Processes -----
        // "/" types a character, so it must not fire while the filter box already has focus — otherwise
        // the key is swallowed and a "/" can never be typed into the box it just focused.
        new(ShortcutId.FocusFilter, [new KeyGesture(Key.OemQuestion)],
            "/", "Focus the process filter", ShortcutScope.Processes, AllowInTextInput: false),

        new(ShortcutId.EndTask, [new KeyGesture(Key.Delete)],
            "Delete", "End the selected process (asks first)", ShortcutScope.Processes,
            AllowInTextInput: false),

        new(ShortcutId.SortAscending, [new KeyGesture(Key.Up, KeyModifiers.Alt)],
            "Alt+↑", "Sort the active column ascending", ShortcutScope.Processes,
            AllowInTextInput: false),

        new(ShortcutId.SortDescending, [new KeyGesture(Key.Down, KeyModifiers.Alt)],
            "Alt+↓", "Sort the active column descending", ShortcutScope.Processes,
            AllowInTextInput: false),

        // ----- File Explorer -----
        // Back/forward and up are separate moves once there is a history trail, so they follow the
        // Windows Explorer convention rather than doubling Alt+← up as "up".
        new(ShortcutId.NavigateBack, [new KeyGesture(Key.Left, KeyModifiers.Alt)],
            "Alt+←", "Go back to the previous folder", ShortcutScope.FileExplorer,
            AllowInTextInput: false),

        new(ShortcutId.NavigateForward, [new KeyGesture(Key.Right, KeyModifiers.Alt)],
            "Alt+→", "Go forward again", ShortcutScope.FileExplorer,
            AllowInTextInput: false),

        // Alt+↑ also sorts on Processes: a gesture may mean different things on different tabs, since
        // only one tab is ever current (see TryResolve).
        new(ShortcutId.NavigateUp,
            [new KeyGesture(Key.Up, KeyModifiers.Alt), new KeyGesture(Key.Back)],
            "Backspace / Alt+↑", "Go up to the parent folder", ShortcutScope.FileExplorer,
            AllowInTextInput: false),

        // "/" is the tab-local focus gesture here too — the address bar is this page's typing field.
        // Text-unsafe for the same reason as the Processes filter, which also parks Ctrl+L while a box
        // has focus; the only text box on this page is the path box, and it already ignores Ctrl+L.
        new(ShortcutId.FocusAddressBar,
            [new KeyGesture(Key.L, KeyModifiers.Control), new KeyGesture(Key.OemQuestion)],
            "Ctrl+L or /", "Edit the folder path", ShortcutScope.FileExplorer,
            AllowInTextInput: false),

        // ----- Network -----
        // Ctrl is required so PageUp/PageDown and the bare arrows keep scrolling the connections list.
        new(ShortcutId.PreviousPage, [new KeyGesture(Key.Left, KeyModifiers.Control)],
            "Ctrl+←", "Previous page of connections", ShortcutScope.Network),

        new(ShortcutId.NextPage, [new KeyGesture(Key.Right, KeyModifiers.Control)],
            "Ctrl+→", "Next page of connections", ShortcutScope.Network),

        // ----- Toolkit -----
        // The same action as the Processes filter, on the tab that owns a different box — the shell
        // offers a resolved id to the current page, so one id serves both. Text-guarded like that one.
        new(ShortcutId.FocusFilter, [new KeyGesture(Key.OemQuestion)],
            "/", "Focus the command filter", ShortcutScope.Toolkit, AllowInTextInput: false),

        // ----- Layout -----
        // Ctrl+Shift rather than Alt+arrow, which Processes uses to sort and File Explorer to go up.
        new(ShortcutId.MoveItemBack,
            [new KeyGesture(Key.Left, KeyModifiers.Control | KeyModifiers.Shift)],
            "Ctrl+Shift+←", "Move the focused widget or card one slot earlier", ShortcutScope.Global),

        new(ShortcutId.MoveItemForward,
            [new KeyGesture(Key.Right, KeyModifiers.Control | KeyModifiers.Shift)],
            "Ctrl+Shift+→", "Move it one slot later", ShortcutScope.Global),
    ];

    /// <summary>The default bindings grouped as the Help modal renders them. Generated from
    /// <see cref="All"/> rather than hand-maintained, so documentation cannot drift away from what the
    /// keys actually do. <b>The shell renders <c>ShortcutBindings.HelpGroups</c> instead</b>, which is
    /// this same grouping over the user's chosen bindings; this one is the shipped baseline.</summary>
    public static IReadOnlyList<ShortcutGroup> HelpGroups { get; } = GroupForHelp(All);

    /// <summary>Groups any binding table for Help. Takes the table rather than reading <see cref="All"/>
    /// so <c>ShortcutBindings</c> can group the effective bindings through the same code, instead of
    /// growing a second copy of this that could drift.</summary>
    public static IReadOnlyList<ShortcutGroup> GroupForHelp(IReadOnlyList<Shortcut> shortcuts) {
        var groups = new List<ShortcutGroup>();

        // The scopes are declared general-first, which is also the order to read them in.
        foreach (var scope in Enum.GetValues<ShortcutScope>()) {
            var rows = new List<Shortcut>();
            foreach (var shortcut in shortcuts)
                if (shortcut.Scope == scope && shortcut.ShowInHelp)
                    rows.Add(shortcut);

            if (rows.Count > 0)
                groups.Add(new ShortcutGroup(TitleOf(scope), rows));
        }

        return groups;
    }

    /// <summary>The heading a scope reads as in Help.</summary>
    private static string TitleOf(ShortcutScope scope) => scope switch {
        ShortcutScope.Search => "Search",
        ShortcutScope.Processes => "Processes",
        ShortcutScope.FileExplorer => "File Explorer",
        ShortcutScope.Network => "Network",
        ShortcutScope.Toolkit => "Toolkit",
        _ => "General",
    };

    /// <summary>Builds one of the Ctrl+digit tab jumps. Only the first carries Help copy — one row
    /// covers the run (see <see cref="Shortcut.ShowInHelp"/>).</summary>
    private static Shortcut Tab(ShortcutId id, Key digit, Key numpad, string keys = "", string description = "") =>
        new(id,
            [new KeyGesture(digit, KeyModifiers.Control), new KeyGesture(numpad, KeyModifiers.Control)],
            keys, description, ShortcutScope.Global, ShowInHelp: keys.Length > 0);

    /// <summary>
    /// Resolves a key press to the action it triggers on the tab named by <paramref name="scope"/>.
    ///
    /// A gesture may appear in more than one scope — Alt+↑ sorts on Processes and climbs a folder on
    /// File Explorer — because only one tab is ever current. The active tab's binding therefore wins,
    /// and a global one is used only when that tab claims nothing.
    ///
    /// <paramref name="textInputFocused"/> suppresses every shortcut that isn't safe to steal from a
    /// text box, so typing "/" into a search field never fires an app action.
    /// </summary>
    public static bool TryResolve(
        Key key, KeyModifiers modifiers, bool textInputFocused, ShortcutScope scope, out ShortcutId id) =>
        TryResolve(All, key, modifiers, textInputFocused, scope, out id);

    /// <summary>The same resolution over any binding table, so <c>ShortcutBindings</c> can run the user's
    /// chosen bindings through this code rather than through a second copy of it.</summary>
    public static bool TryResolve(IReadOnlyList<Shortcut> shortcuts,
        Key key, KeyModifiers modifiers, bool textInputFocused, ShortcutScope scope, out ShortcutId id) {
        if (scope != ShortcutScope.Global &&
            TryMatch(shortcuts, key, modifiers, textInputFocused, scope, out id))
            return true;

        return TryMatch(shortcuts, key, modifiers, textInputFocused, ShortcutScope.Global, out id);
    }

    private static bool TryMatch(IReadOnlyList<Shortcut> shortcuts,
        Key key, KeyModifiers modifiers, bool textInputFocused, ShortcutScope scope, out ShortcutId id) {
        foreach (var shortcut in shortcuts) {
            if (shortcut.Scope != scope)
                continue;
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
