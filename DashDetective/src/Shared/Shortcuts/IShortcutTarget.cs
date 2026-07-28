namespace DashDetective.Shared.Shortcuts;

/// <summary>A page that handles keyboard shortcuts of its own. The shell offers every resolved
/// shortcut to the current page before acting on it globally, so a page opts in simply by implementing
/// this — no per-page wiring in the shell. Mirrors <see cref="IRefreshablePage"/>.</summary>
public interface IShortcutTarget {
    /// <summary>Runs the shortcut if this page owns it, returning whether it was consumed. Returning
    /// <c>false</c> lets the shell fall back to the global handling for that id.</summary>
    bool HandleShortcut(ShortcutId id);
}
