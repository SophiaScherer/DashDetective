using System;
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

        // ----- Diagnostics -----
        // The first rows whose output lands in the Execution Log. All run as the current user — none of
        // these needs admin, which is why they can be captured at all (an elevated run cannot be).
        Diagnostic("ipconfig /all", "Full IP configuration for every adapter",
                   ToolkitAction.Capture("ipconfig", "/all")),
        Diagnostic("ipconfig /flushdns", "Clears the DNS resolver cache",
                   ToolkitAction.Capture("ipconfig", "/flushdns")),

        // A cold systeminfo routinely runs past the default 20s — it queries the hotfix list and the
        // network stack — so it gets its own limit rather than being reported as a timeout.
        Diagnostic("systeminfo", "Full OS, hardware and hotfix summary",
                   ToolkitAction.Capture("systeminfo").WithTimeout(TimeSpan.FromSeconds(90))),

        // The only two rows that take input. The host is validated before it is appended and reaches
        // the OS as its own argument, so it can never become a flag — see ToolkitHostValidator.
        Diagnostic("ping <host>", "Sends four echo requests and reports the round trip",
                   ToolkitAction.Capture("ping").WithTimeout(TimeSpan.FromSeconds(30)),
                   new ToolkitParameter("host or IP")),

        // Hops are capped: an uncapped tracert probes 30 hops × 3 packets and can sit there for
        // minutes with the page disabled behind it. The log's "$" line shows the flag, so what ran is
        // never a surprise even though the label leaves it out.
        Diagnostic("tracert <host>", "Traces the route to a host, up to 20 hops",
                   ToolkitAction.Capture("tracert", "-h", "20").WithTimeout(TimeSpan.FromSeconds(120)),
                   new ToolkitParameter("host or IP")),

        // The one entry that needs admin. Elevated rather than captured because Windows refuses to
        // redirect a runas process's streams — it runs in its own console window, and the log says so.
        // It also runs for many minutes, which a captured command's timeout would cut short.
        Diagnostic("sfc /scannow", "Scans and repairs protected system files — needs administrator",
                   ToolkitAction.Elevated("sfc", "/scannow")),

        // ----- Docs & Links -----
        // Each one backs a row above it, so the tab explains as well as runs. Every URL was checked to
        // resolve when it was authored; the runner refuses anything that is not https:// regardless.
        Doc("Windows commands A–Z", "Reference for every built-in console command",
            "https://learn.microsoft.com/en-us/windows-server/administration/windows-commands/windows-commands"),
        Doc("ipconfig reference", "Every ipconfig switch, including /all and /flushdns",
            "https://learn.microsoft.com/en-us/windows-server/administration/windows-commands/ipconfig"),
        Doc("sfc reference", "What System File Checker scans, and the repair options it takes",
            "https://learn.microsoft.com/en-us/windows-server/administration/windows-commands/sfc"),
        Doc("Device Manager error codes", "What each yellow-exclamation code on a device means",
            "https://learn.microsoft.com/en-us/windows-hardware/drivers/install/device-manager-error-messages"),
        Doc("Known folder reference", "What AppData, Roaming, Local and the rest are actually for",
            "https://learn.microsoft.com/en-us/windows/win32/shell/knownfolderid"),
        Doc("wevtutil reference", "Query, export and clear the event logs from the command line",
            "https://learn.microsoft.com/en-us/windows-server/administration/windows-commands/wevtutil"),
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

    /// <summary>A console command whose output is captured into the Execution Log. The action is passed
    /// in rather than derived from the label: the row reads as one command line, but the runner needs it
    /// already split into a file name and separate arguments.</summary>
    private static ToolkitEntry Diagnostic(
        string command, string description, ToolkitAction action, ToolkitParameter? parameter = null) =>
        new(command, description, ToolkitCategory.Diagnostics, ToolkitEntryKind.Command, action,
            parameter);

    /// <summary>A documentation link. Labelled by title rather than URL — a Learn URL would ellipsize
    /// to nothing in the row's mono label. The URL still shows in the Execution Log's "$" line, so what
    /// was opened is on the record.</summary>
    private static ToolkitEntry Doc(string title, string description, string url) =>
        new(title, description, ToolkitCategory.DocsAndLinks, ToolkitEntryKind.Link,
            ToolkitAction.OpenUrl(url));

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
