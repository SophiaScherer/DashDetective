using System.Collections.Generic;

namespace DashDetective.Tabs.Toolkit;

/// <summary>
/// The catalog for a platform that has no table yet — the genuine third arm, not "not Windows". It
/// offers nothing rather than offering another platform's rows: the tab falls back to its own "no
/// commands" empty state, and the user can still author their own commands, which is a better answer
/// than thirty rows that can only fail.
/// </summary>
internal sealed class UnsupportedToolkitCatalog : IToolkitCatalog {
    internal static UnsupportedToolkitCatalog Instance { get; } = new();

    private UnsupportedToolkitCatalog() { }

    public IReadOnlyList<ToolkitEntry> Entries { get; } = [];
}
