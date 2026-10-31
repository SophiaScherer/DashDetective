using Avalonia.Input;
using DashDetective.Shared.Shortcuts;
using DashDetective.Shell.Help;
using DashDetective.Tabs.Settings;
using System;

namespace DashDetective.Shell;

/// <summary>
/// What the mouse's back and forward buttons do, decided in the shell chain's order: a shortcut capture,
/// then an open modal, then a focused text box, then the current tab's own step, and for back only, page
/// history. Split out, like <see cref="ModalShortcuts"/>, because <see cref="MainWindowViewModel"/> cannot
/// be constructed in a test.
/// </summary>
internal static class MouseNavigation {
    /// <summary>Runs a mouse button press, returning whether it was consumed. <paramref name="goBack"/>
    /// steps page history and reports whether it moved.</summary>
    public static bool Handle(PointerUpdateKind button, bool textInputFocused, bool capturing,
        HelpViewModel help, AccentPickerViewModel picker, IShortcutTarget? page, Func<bool> goBack) {
        // Back falls through to page history; forward has no page trail, so it is the tab's alone.
        ShortcutId tabStep;
        Func<bool>? fallback;
        switch (button) {
            case PointerUpdateKind.XButton1Pressed:
                (tabStep, fallback) = (ShortcutId.NavigateBack, goBack);
                break;
            case PointerUpdateKind.XButton2Pressed:
                (tabStep, fallback) = (ShortcutId.NavigateForward, null);
                break;
            default:
                return false;
        }

        // The capture box listens for keys only; leave the press alone rather than act behind it.
        if (capturing)
            return false;

        // Behind a scrim nothing navigates. Swallowed, so the press cannot dismiss the modal either.
        if (ModalShortcuts.Handle(tabStep, help, picker) is { } consumed)
            return consumed;

        // The same guard Alt+← has: a field being edited (File Explorer's path box) is not left mid-edit.
        if (textInputFocused)
            return false;

        if (page?.HandleShortcut(tabStep) == true)
            return true;

        return fallback?.Invoke() ?? false;
    }
}
