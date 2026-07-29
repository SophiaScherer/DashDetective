namespace DashDetective.Shared.Shortcuts;

/// <summary>A page that handles keyboard shortcuts of its own. The shell offers every resolved
/// shortcut to the current page before acting on it globally, so a page opts in simply by implementing
/// this — no per-page wiring in the shell. Mirrors <see cref="IRefreshablePage"/>.</summary>
public interface IShortcutTarget {
    /// <summary>Which set of bindings applies while this page is showing. Naming its own scope is what
    /// lets two tabs bind the same gesture to different actions.</summary>
    ShortcutScope Scope { get; }

    /// <summary>Runs the shortcut if this page owns it, returning whether it was consumed. Returning
    /// <c>false</c> lets the shell fall back to the global handling for that id.</summary>
    bool HandleShortcut(ShortcutId id);
}
