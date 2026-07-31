using System.Collections.Generic;

namespace DashDetective.Tabs.Toolkit;

/// <summary>
/// The Toolkit tab's copy and its command table, held as a static table like <c>HardwareCatalog</c>
/// and <c>SettingCatalog</c> so both are testable without a UI. The view binds these strings rather
/// than holding literals, so what is on screen is by construction what search matches against.
///
/// <see cref="Entries"/> is deliberately **empty**: the tab ships as UI only, and the command set is
/// authored later. This is the one seam that changes when it is — nothing else needs touching.
/// </summary>
public static class ToolkitCatalog {
    /// <summary>Every command, in no particular order — the list groups them by category.</summary>
    public static IReadOnlyList<ToolkitEntry> Entries { get; } = [];

    /// <summary>The categories in display order, matching the enum's declaration order.</summary>
    public static IReadOnlyList<ToolkitCategory> Categories { get; } = [
        ToolkitCategory.FileLocations,
        ToolkitCategory.SystemTools,
        ToolkitCategory.Terminal,
        ToolkitCategory.Maintenance,
    ];

    /// <summary>The label a category reads as, on its section header and its filter chip.</summary>
    public static string HeaderFor(ToolkitCategory category) => category switch {
        ToolkitCategory.FileLocations => "File Locations",
        ToolkitCategory.SystemTools => "System Tools",
        ToolkitCategory.Terminal => "Terminal",
        _ => "Maintenance",
    };

    /// <summary>The badge a kind reads as, on the row beside the command.</summary>
    public static string LabelFor(ToolkitEntryKind kind) => kind switch {
        ToolkitEntryKind.Folder => "Folder",
        ToolkitEntryKind.App => "App",
        ToolkitEntryKind.Panel => "Panel",
        _ => "Command",
    };
}
