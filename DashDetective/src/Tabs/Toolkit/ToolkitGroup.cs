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

    /// <summary>The uppercase section label above the card.</summary>
    public string Header => ToolkitCatalog.HeaderFor(Category);

    public IReadOnlyList<ToolkitEntry> Items { get; }
}
