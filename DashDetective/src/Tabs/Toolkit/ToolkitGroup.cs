using System.Collections.Generic;

namespace DashDetective.Tabs.Toolkit;

/// <summary>
/// One category section of the command list: its header and the entries that survived the filter.
/// Immutable — the view model rebuilds the whole group list when the filter changes, so a group
/// never needs to mutate.
/// </summary>
public sealed class ToolkitGroup {
    public ToolkitGroup(ToolkitCategory category, IReadOnlyList<ToolkitEntry> items) {
        Category = category;
        Items = items;
    }

    public ToolkitCategory Category { get; }

    /// <summary>The section label above the card. Upper-cased here rather than in the catalog: the
    /// chips show the same names in title case, and Avalonia has no text-transform.</summary>
    public string Header => ToolkitCatalog.HeaderFor(Category).ToUpperInvariant();

    public IReadOnlyList<ToolkitEntry> Items { get; }
}
