using DashDetective.Services.Platform.Linux;
using System;

namespace DashDetective.Tabs.Processes;

/// <summary>
/// Sorts a Linux process into the same three groups <see cref="ProcessClassifier"/> derives on Windows, from
/// <c>/proc/[pid]/cgroup</c> rather than from window ownership. <b>The X11 route is a dead end</b> — the
/// target desktop is GNOME on Wayland, where no client may enumerate another client's windows by design — but
/// on any systemd distro the cgroup path already encodes the distinction, is world-readable, and needs
/// neither root nor a display server.
///
/// Rules, in order: kernel thread → System; root-owned or <c>system.slice</c> → System; a <c>.service</c>
/// leaf → Background; <c>app.slice</c> or an <c>app-*.scope</c> leaf → App; otherwise Background.
///
/// Two documented divergences: on a non-systemd distro there is no unified cgroup line, so everything
/// user-owned reads as Background; and a process started from a terminal inherits that terminal's app scope
/// and reads as App — arguably right, since it is user-initiated. A GUI app launched with <c>sudo</c> is
/// owned by root and so reads as System, where Task Manager would still call an elevated app an app.
///
/// Pure, so the whole rule table is testable from a Windows box. Reached only from
/// <see cref="LinuxProcessSnapshotProvider"/>.
/// </summary>
internal static class LinuxProcessClassifier {
    /// <summary>kthreadd, which forks every kernel thread on the machine.</summary>
    private const int KernelThreadDaemonPid = 2;
    private const int RootUid = 0;
    private const char ZombieState = 'Z';

    private const string SystemSlice = "/system.slice/";
    private const string AppSlice = "/app.slice/";
    private const string ServiceSuffix = ".service";
    private const string ScopePrefix = "app-";
    private const string ScopeSuffix = ".scope";

    /// <param name="hasCommandLine">Whether <c>/proc/[pid]/cmdline</c> held anything. Empty means no
    /// user-space address space.</param>
    /// <param name="cgroup">The unified cgroup path from <see cref="ProcCgroupParser"/>, or <c>""</c> when
    /// the host runs cgroup v1 only.</param>
    internal static ProcessCategory Classify(
        int pid, int parentPid, char state, int? uid, bool hasCommandLine, string cgroup) {
        if (IsKernelThread(pid, parentPid, state, hasCommandLine))
            return ProcessCategory.Windows;

        // A service running as its own non-root user (systemd-resolve, polkitd) is still a system process,
        // which is why the slice is checked alongside the owner rather than instead of it.
        if (uid == RootUid || cgroup.Contains(SystemSlice, StringComparison.Ordinal))
            return ProcessCategory.Windows;

        var leaf = ProcCgroupParser.Leaf(cgroup);

        // .service is tested BEFORE app.slice: modern systemd puts user *units* inside app.slice too, so
        // matching the slice first would file every user daemon as a foreground app.
        if (leaf.EndsWith(ServiceSuffix, StringComparison.Ordinal))
            return ProcessCategory.Background;

        return cgroup.Contains(AppSlice, StringComparison.Ordinal) || IsAppScope(leaf)
            ? ProcessCategory.App
            : ProcessCategory.Background;
    }

    /// <summary>A kernel thread has no address space, so its <c>cmdline</c> is empty — the general test,
    /// since kthreadd's descendants are not all its direct children. <b>A zombie's cmdline is empty too</b>,
    /// but it is the corpse of a user process rather than a kernel thread, and its cgroup still says which.
    /// </summary>
    private static bool IsKernelThread(int pid, int parentPid, char state, bool hasCommandLine) =>
        pid == KernelThreadDaemonPid
        || parentPid == KernelThreadDaemonPid
        || (!hasCommandLine && state != ZombieState);

    /// <summary>GNOME launches each desktop app into its own <c>app-gnome-firefox-3456.scope</c>.</summary>
    private static bool IsAppScope(string leaf) =>
        leaf.StartsWith(ScopePrefix, StringComparison.Ordinal)
        && leaf.EndsWith(ScopeSuffix, StringComparison.Ordinal);
}
