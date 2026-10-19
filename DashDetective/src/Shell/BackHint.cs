using DashDetective.Shared.Shortcuts;

namespace DashDetective.Shell;

/// <summary>
/// What the toolbar's Back button says. It names the page a click opens, and the key read from the live
/// bindings, so a rebind is reflected in the tooltip too. Pure, like <see cref="RefreshHint"/>, so it is
/// testable without a constructed shell.
/// </summary>
internal static class BackHint {
    /// <summary>The tooltip, given the title of the page Back would open (null at the start of history).</summary>
    public static string For(string? previousTitle, ShortcutBindings bindings) {
        var keys = bindings.KeysFor(ShortcutId.Back);
        var action = previousTitle is null ? "Back" : $"Back to {previousTitle}";
        return keys.Length == 0 ? action : $"{action} ({keys})";
    }
}
