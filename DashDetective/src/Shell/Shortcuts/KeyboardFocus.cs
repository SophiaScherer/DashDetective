using Avalonia.Controls;

namespace DashDetective.Shell.Shortcuts;

/// <summary>Focus questions the shortcut dispatcher needs to ask before acting on a key.</summary>
public static class KeyboardFocus {
    /// <summary>Whether a text-entry control holds focus. Bare-key shortcuts are suppressed while it
    /// does, so the key reaches the field the user is typing into instead of firing an app action.</summary>
    public static bool IsTextInputFocused(TopLevel? top) =>
        top?.FocusManager?.GetFocusedElement() is TextBox;
}
