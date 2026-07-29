namespace DashDetective.Shared.Shortcuts;

/// <summary>Where a shortcut applies. Global shortcuts work anywhere; the rest are offered only to the
/// tab that owns them, via <see cref="IShortcutTarget"/>.</summary>
public enum ShortcutScope {
    Global,
    Processes,
    FileExplorer,
    Network,
}
