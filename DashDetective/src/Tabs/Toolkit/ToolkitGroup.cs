using System.Collections.Generic;

namespace DashDetective.Tabs.Toolkit;

/// <summary>
/// One section of the command list: its header and the entries that survived the filter. Usually a
/// category, but the pinned section is a section without one — which is why <see cref="Category"/> is
/// nullable rather than the enum gaining a pseudo-value that the filter chips would then have to
/// exclude by hand.
///
/// Immutable — the view model rebuilds the whole group list when the filter changes, so a group never
/// needs to mutate.
/// </summary>
public sealed class ToolkitGroup {
    /// <summary>The pinned section's header. Already upper-cased, like the category headers.</summary>
    public const string PinnedHeader = "PINNED";

    public ToolkitGroup(ToolkitCategory category, IReadOnlyList<ToolkitEntry> items)
        : this(ToolkitCatalog.HeaderFor(category).ToUpperInvariant(), items) {
        Category = category;
    }

    private ToolkitGroup(string header, IReadOnlyList<ToolkitEntry> items) {
        Header = header;
        Items = items;
    }

    /// <summary>The section for the user's pinned commands, which sits above every category.</summary>
    public static ToolkitGroup Pinned(IReadOnlyList<ToolkitEntry> items) =>
        new(PinnedHeader, items);

    /// <summary>The category this section shows, or null for the pinned section.</summary>
    public ToolkitCategory? Category { get; }

    /// <summary>The section label above the card. Upper-cased when the group is built rather than in the
    /// catalog: the chips show the same names in title case, and Avalonia has no text-transform.</summary>
    public string Header { get; }

    public IReadOnlyList<ToolkitEntry> Items { get; }
}
