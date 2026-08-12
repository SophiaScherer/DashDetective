using System;
using System.Collections.Generic;
using static DashDetective.Tabs.Toolkit.ToolkitRows;

namespace DashDetective.Tabs.Toolkit;

/// <summary>
/// The built-in command set on Linux. Written for a GNOME desktop, which is what the tool rows assume;
/// everything in Diagnostics is either coreutils, iproute2 or systemd, so it is there on any modern
/// distro.
///
/// <b>Exactly one row is elevated</b>, and this table is the only place one can be:
/// <see cref="ToolkitCommandType"/> has no elevated member, so no user-authored row can raise a prompt.
/// <see cref="ToolkitActionKind.Elevated"/> reaches the OS as <c>pkexec</c> here rather than the Windows
/// <c>runas</c> verb — see <see cref="SystemProcessLauncher.BuildLaunchInfo"/>. A declined polkit prompt
/// is silent rather than worded, which is the one thing the Windows side does that this does not.
///
/// <b>A missing program is a run-time answer, not a catalog-time one.</b> Distros disagree about what
/// is installed, and filtering this table at startup would mean shelling out once per row before the
/// page could draw. A row whose program is absent fails with the shell's own reason, which is the same
/// thing that happens to a user-authored row.
/// </summary>
internal sealed class LinuxToolkitCatalog : IToolkitCatalog {
    /// <summary>The one catalog — shared for the same reason <see cref="WindowsToolkitCatalog"/>'s is:
    /// <see cref="ToolkitEntry.IsPinned"/> is live state on the rows themselves.</summary>
    internal static LinuxToolkitCatalog Instance { get; } = new();

    private LinuxToolkitCatalog() { }

    public IReadOnlyList<ToolkitEntry> Entries { get; } = [
        // ----- Folders -----
        // Left in the notation the user would type: ToolkitPaths.Resolve expands a leading "~" and
        // $VAR at run time, so nothing here bakes in one session's home directory.
        Folder("~", "The current user's home folder"),
        Folder("~/.config", "Per-user application configuration"),
        Folder("~/.local/share", "Per-user application data"),
        Folder("~/.config/autostart", "Programs that run when the current user logs in"),
        Folder("/etc", "System-wide configuration"),
        Folder("/var/log", "System and service log files"),
        Folder("/tmp", "Temporary files, cleared between boots"),

        // ----- System Tools -----
        // Named by the binary rather than the window title, matching how the Windows rows read, with
        // the friendly name in the description — the filter and universal search both match on it, so
        // "network" still finds nm-connection-editor.
        Tool("gnome-system-monitor", "System Monitor — processes, resources and file systems"),
        Tool("gnome-disks", "Disks — partitions, SMART health and mount options"),
        Tool("nm-connection-editor", "Network Connections — adapters and their properties"),

        Panel("gnome-control-center", "Settings — the desktop's control centre"),
        Panel("gnome-software", "Software — installed applications and updates"),

        // ----- Diagnostics -----
        // All run as the current user — none needs root, which is why their output can be captured at
        // all. Deliberately absent: dmesg, which Ubuntu and Debian restrict to root
        // (kernel.dmesg_restrict), so the row could only ever fail. The journal's -k gives the same
        // ring buffer to an ordinary user.
        Diagnostic("hostnamectl", "Host name, OS, kernel and machine ID",
                   ToolkitAction.Capture("hostnamectl")),
        Diagnostic("ip addr", "Every network interface and its addresses",
                   ToolkitAction.Capture("ip", "addr")),
        Diagnostic("ss -tulpn", "Listening TCP and UDP sockets and what is holding them",
                   ToolkitAction.Capture("ss", "-tulpn")),

        // --no-pager is belt-and-braces: these already skip the pager when their output is redirected,
        // which it always is here. The log's "$" line shows the flag either way.
        Diagnostic("systemctl list-units --failed", "Services that failed to start",
                   ToolkitAction.Capture("systemctl", "list-units", "--failed", "--no-pager")),
        Diagnostic("journalctl -p err -n 50", "The last 50 error-level journal entries",
                   ToolkitAction.Capture("journalctl", "-p", "err", "-n", "50", "--no-pager")),
        Diagnostic("journalctl -k -n 50", "The last 50 kernel messages",
                   ToolkitAction.Capture("journalctl", "-k", "-n", "50", "--no-pager")),

        Diagnostic("df -h", "Free space on every mounted filesystem",
                   ToolkitAction.Capture("df", "-h")),
        Diagnostic("lsblk", "Block devices, partitions and where they are mounted",
                   ToolkitAction.Capture("lsblk")),
        Diagnostic("free -h", "Memory and swap in use",
                   ToolkitAction.Capture("free", "-h")),
        Diagnostic("resolvectl status", "DNS servers and search domains, per link",
                   ToolkitAction.Capture("resolvectl", "status")),

        // The one entry that needs root. Elevated rather than captured because pkexec's child does not
        // inherit the redirected streams usefully, and the metadata download runs longer than a
        // captured command's timeout allows. fwupd is preinstalled on Ubuntu and Fedora GNOME; where it
        // is not, the row fails with the shell's own "not found" like every other tool row.
        Diagnostic("fwupdmgr refresh", "Refreshes the firmware update metadata — needs administrator",
                   ToolkitAction.Elevated("fwupdmgr", "refresh")),

        // The one row that takes input. "-c 4" is not cosmetic: Linux ping runs until interrupted, so
        // without it every run would end at the timeout instead of reporting. The typed host is
        // validated and appended as its own argument, so it can never become a flag — see
        // ToolkitHostValidator.
        Diagnostic("ping <host>", "Sends four echo requests and reports the round trip",
                   ToolkitAction.Capture("ping", "-c", "4").WithTimeout(TimeSpan.FromSeconds(30)),
                   new ToolkitParameter("host or IP")),

        // ----- Docs & Links -----
        // Each one backs a row above it, so the tab explains as well as runs. Every URL was fetched and
        // confirmed to resolve over https when it was authored; the runner refuses anything that is not
        // https:// regardless. systemd's own pages are on freedesktop.org, but man7.org mirrors them
        // and answers a plain fetch, so the whole set can be checked the same way.
        Doc("Linux man pages", "Every command's manual page, online",
            "https://man7.org/linux/man-pages/index.html"),
        Doc("ip reference", "Every ip subcommand, including addr, route and link",
            "https://man7.org/linux/man-pages/man8/ip.8.html"),
        Doc("ss reference", "What each ss filter and flag selects, including -tulpn",
            "https://man7.org/linux/man-pages/man8/ss.8.html"),
        Doc("systemctl reference", "Inspecting, starting and stopping units",
            "https://man7.org/linux/man-pages/man1/systemctl.1.html"),
        Doc("journalctl reference", "Filtering the journal by priority, unit, boot and time",
            "https://man7.org/linux/man-pages/man1/journalctl.1.html"),
        Doc("XDG Base Directory specification", "What ~/.config, ~/.local/share and the rest are for",
            "https://specifications.freedesktop.org/basedir/latest/"),
        Doc("Filesystem Hierarchy Standard", "What lives in /etc, /var, /tmp and the rest of the tree",
            "https://refspecs.linuxfoundation.org/FHS_3.0/fhs/index.html"),
    ];
}
