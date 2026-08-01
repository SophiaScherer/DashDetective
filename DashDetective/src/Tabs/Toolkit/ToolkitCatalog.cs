using System.Collections.Generic;

namespace DashDetective.Tabs.Toolkit;

/// <summary>
/// The Toolkit tab's copy and its command table, held as a static table like <c>HardwareCatalog</c>
/// and <c>SettingCatalog</c> so both are testable without a UI. The view binds these strings rather
/// than holding literals, so what is on screen is by construction what search matches against.
///
/// <see cref="Entries"/> is also the app's **allow-list**: <see cref="ToolkitRunner"/> only ever runs a
/// <see cref="ToolkitAction"/> authored here, and there is no free-form command entry anywhere in the
/// UI. Adding a row here is the only way to make something runnable.
/// </summary>
public static class ToolkitCatalog {
    /// <summary>Every command, in no particular order — the list groups them by category.</summary>
    public static IReadOnlyList<ToolkitEntry> Entries { get; } = [
        // ----- Folders -----
        // Environment variables are left unexpanded: the runner resolves them at run time, and the row
        // deliberately reads as the shortcut the user would type themselves.
        Folder("%appdata%", "Roaming application data for the current user"),
        Folder("%localappdata%", "Machine-specific application data for the current user"),
        Folder("%temp%", "The current user's temporary files"),
        Folder("%userprofile%", "The current user's home folder"),
        Folder("%programdata%", "Application data shared by all users"),
        Folder("%windir%", "The Windows installation folder"),
        Folder("shell:startup", "Programs that run when the current user signs in"),

        // The folder, not the file: hosts has no default association, so opening the file itself would
        // either fail or raise the "how do you want to open this?" picker.
        Folder(@"%windir%\System32\drivers\etc", "The folder holding the hosts file"),
    ];

    /// <summary>A row that opens a folder in Explorer — the shape every Folders entry takes.</summary>
    private static ToolkitEntry Folder(string path, string description) =>
        new(path, description, ToolkitCategory.Folders, ToolkitEntryKind.Folder,
            ToolkitAction.OpenPath(path));

    /// <summary>The categories in display order, matching the enum's declaration order.</summary>
    public static IReadOnlyList<ToolkitCategory> Categories { get; } = [
        ToolkitCategory.Folders,
        ToolkitCategory.SystemTools,
        ToolkitCategory.Diagnostics,
        ToolkitCategory.DocsAndLinks,
    ];

    /// <summary>The label a category reads as, on its section header and its filter chip.</summary>
    public static string HeaderFor(ToolkitCategory category) => category switch {
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
