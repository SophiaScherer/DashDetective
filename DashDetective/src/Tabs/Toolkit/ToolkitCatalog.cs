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

        // ----- System Tools -----
        // Each is named by the command you would type in Run, with the friendly name in the description
        // — the filter and universal search both match on the description, so "task scheduler" still
        // finds taskschd.msc without the user knowing what it is called.
        Tool("taskschd.msc", "Task Scheduler — create and inspect scheduled tasks"),
        Tool("services.msc", "Services — start, stop and configure Windows services"),
        Tool("devmgmt.msc", "Device Manager — installed hardware and driver status"),
        Tool("eventvwr.msc", "Event Viewer — system, application and security logs"),
        Tool("regedit", "Registry Editor — browse and edit the Windows registry"),
        Tool("cleanmgr", "Disk Cleanup — reclaim space from temporary and system files"),
        Tool("resmon", "Resource Monitor — live CPU, memory, disk and network detail"),
        Tool("msconfig", "System Configuration — boot options and startup services"),
        Tool("dxdiag", "DirectX Diagnostic — graphics, sound and input diagnostics"),

        Panel("ncpa.cpl", "Network Connections — adapters and their properties"),
        Panel("appwiz.cpl", "Programs and Features — installed programs"),
    ];

    /// <summary>A row that opens a folder in Explorer — the shape every Folders entry takes.</summary>
    private static ToolkitEntry Folder(string path, string description) =>
        new(path, description, ToolkitCategory.Folders, ToolkitEntryKind.Folder,
            ToolkitAction.OpenPath(path));

    /// <summary>A standalone tool or MMC console. Launched through the shell rather than resolved to a
    /// full path, so the <c>.msc</c> association picks up <c>mmc</c> and the bare names resolve off
    /// <c>System32</c> on the PATH — the same way typing them into Run does.</summary>
    private static ToolkitEntry Tool(string command, string description) =>
        new(command, description, ToolkitCategory.SystemTools, ToolkitEntryKind.App,
            ToolkitAction.Launch(command));

    /// <summary>A Control Panel applet. Same launch path as <see cref="Tool"/>; the separate kind is
    /// what gives it its own badge and colour.</summary>
    private static ToolkitEntry Panel(string command, string description) =>
        new(command, description, ToolkitCategory.SystemTools, ToolkitEntryKind.Panel,
            ToolkitAction.Launch(command));

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
