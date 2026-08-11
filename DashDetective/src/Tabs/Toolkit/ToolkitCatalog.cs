using System.Collections.Generic;

namespace DashDetective.Tabs.Toolkit;

/// <summary>
/// The Toolkit tab's copy, held as a static table like <c>HardwareCatalog</c> and
/// <c>SettingCatalog</c> so it is testable without a UI. The view binds these strings rather than
/// holding literals, so what is on screen is by construction what search matches against.
///
/// The copy here reads the same on every platform, which is why it is static. The command set does
/// not, and lives behind <see cref="IToolkitCatalog"/> instead — see
/// <see cref="IToolkitCatalog.ForCurrentPlatform"/>. What the filter, the pins and universal search
/// all read is <see cref="ToolkitViewModel.AllEntries"/>, which merges that platform's rows with the
/// user's own (<see cref="ToolkitCommand"/>).
/// </summary>
public static class ToolkitCatalog {
    /// <summary>The categories in display order, matching the enum's declaration order.</summary>
    public static IReadOnlyList<ToolkitCategory> Categories { get; } = [
        ToolkitCategory.Custom,
        ToolkitCategory.Folders,
        ToolkitCategory.SystemTools,
        ToolkitCategory.Diagnostics,
        ToolkitCategory.DocsAndLinks,
    ];

    /// <summary>The label a category reads as, on its section header and its filter chip.</summary>
    public static string HeaderFor(ToolkitCategory category) => category switch {
        ToolkitCategory.Custom => "My Commands",
        ToolkitCategory.Folders => "Folders",
        ToolkitCategory.SystemTools => "System Tools",
        ToolkitCategory.Diagnostics => "Diagnostics",
        _ => "Docs & Links",
    };

    /// <summary>The badge a kind reads as, on the row beside the command.</summary>
    public static string LabelFor(ToolkitEntryKind kind) => kind switch {
        ToolkitEntryKind.Folder => "Folder",
        ToolkitEntryKind.App => "App",
        ToolkitEntryKind.Panel => "Panel",
        ToolkitEntryKind.Link => "Link",
        _ => "Command",
    };
}
