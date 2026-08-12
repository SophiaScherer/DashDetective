using DashDetective.Services.Platform.Linux;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace DashDetective.Tabs.Processes;

/// <summary>
/// The process table's OS operations on Linux. Mirrors File Explorer's <c>LinuxShellInterop</c> —
/// duplicated tab-local rather than shared, the same self-contained-tab rule that has
/// <see cref="WindowsProcessInterop"/> declaring its own <c>SHObjectProperties</c> rather than reusing
/// the shell's.
///
/// <b>No I/O counters.</b> <see cref="TryGetIoBytes"/> reports nothing, which is inert rather than a
/// loss: <c>LinuxProcessSnapshotProvider</c> does not take this seam at all and reads its per-process
/// disk bytes straight from <c>/proc/[pid]/io</c>.
///
/// Carries no <c>[SupportedOSPlatform]</c>: it is portable managed code over
/// <see cref="IProcFileSystem"/>, so there is no annotated API for CA1416 to see.
/// </summary>
internal sealed class LinuxProcessInterop : IProcessInterop {
    // Concatenated forward-slash literals, never Path.Combine — see IProcFileSystem.
    private const string ProcRoot = "/proc/";

    private readonly IProcFileSystem _proc;

    public LinuxProcessInterop() : this(new ProcFileSystem()) { }

    /// <summary>Test seam: injects the filesystem so the executable lookup runs against canned fixtures
    /// from any dev machine.</summary>
    internal LinuxProcessInterop(IProcFileSystem proc) => _proc = proc;

    /// <summary>Always false — the Disk column stays blank through this seam. See the class note.</summary>
    public bool TryGetIoBytes(Process process, out ulong totalBytes) {
        totalBytes = 0;
        return false;
    }

    /// <summary>Opens the folder holding the process's executable, the same answer File Explorer's
    /// Properties button gives: no desktop offers a Properties dialog to a foreign process. The
    /// <paramref name="owner"/> handle is unused for that reason.</summary>
    public void ShowProperties(IntPtr owner, int pid) {
        if (RevealTarget(pid) is not { } target)
            return;

        try {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        } catch {
            // Nothing actionable — the folder simply doesn't open.
        }
    }

    /// <summary>
    /// The folder to reveal for a process, or <c>null</c> when there is nothing to show. Split from the
    /// launch so the lookup is unit-tested without starting a file manager on the machine running the
    /// suite.
    ///
    /// <c>/proc/[pid]/exe</c> is a symlink to the running binary. It reads as nothing for a kernel
    /// thread (which has no executable) and for another user's process (the link is owner-only), and
    /// both simply reveal nothing rather than failing.
    /// </summary>
    internal string? RevealTarget(int pid) {
        var link = ProcRoot + pid.ToString(CultureInfo.InvariantCulture) + "/exe";
        if (_proc.ResolveLink(link) is not { Length: > 0 } exe)
            return null;

        try {
            var parent = Path.GetDirectoryName(exe);
            return string.IsNullOrEmpty(parent) ? exe : parent;
        } catch (ArgumentException) {
            return null; // not a usable path
        }
    }
}
