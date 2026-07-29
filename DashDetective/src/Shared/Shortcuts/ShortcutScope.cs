namespace DashDetective.Shared.Shortcuts;

/// <summary>Where a shortcut applies. Global shortcuts work anywhere; the rest are offered only to the
/// tab that owns them, via <see cref="IShortcutTarget"/>.</summary>
public enum ShortcutScope {
    Global,

    /// <summary>The universal-search result list, live only while its dropdown is open. Not a tab: the
    /// shell reports this scope ahead of the current page so the arrow keys drive the results rather
    /// than the page behind them.</summary>
    Search,

    Processes,
    FileExplorer,
    Network,
}
