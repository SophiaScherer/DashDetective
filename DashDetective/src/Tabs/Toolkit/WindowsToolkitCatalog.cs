using System;
using System.Collections.Generic;
using static DashDetective.Tabs.Toolkit.ToolkitRows;

namespace DashDetective.Tabs.Toolkit;

/// <summary>
/// The built-in command set on Windows. Adding a built-in row still means editing this table and
/// nothing else.
///
/// This table is also where <see cref="ToolkitActionKind.Elevated"/> lives and the only place it can:
/// <see cref="ToolkitCommandType"/> has no elevated member, so no user-authored row can raise a UAC
/// prompt.
///
/// No <c>[SupportedOSPlatform("windows")]</c>: this is a table of string literals with no platform API
/// surface, so the attribute would be decorative rather than load-bearing — and
/// <see cref="Instance"/> is a static field, initialised on class load outside any
/// <c>OperatingSystem.IsWindows()</c> guard, so it could not be honoured anyway.
/// </summary>
internal sealed class WindowsToolkitCatalog : IToolkitCatalog {
    /// <summary>The one catalog. Shared rather than rebuilt per caller because
    /// <see cref="ToolkitEntry.IsPinned"/> is live state on the rows themselves — there is exactly one
    /// Toolkit page, so these <i>are</i> its rows.</summary>
    internal static WindowsToolkitCatalog Instance { get; } = new();

    private WindowsToolkitCatalog() { }

    public IReadOnlyList<ToolkitEntry> Entries { get; } = [
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
}
