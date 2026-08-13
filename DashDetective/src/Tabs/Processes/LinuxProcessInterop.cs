using DashDetective.Services.Platform.Linux;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace DashDetective.Tabs.Processes;

/// <summary>
/// The process table's OS operations on Linux. Duplicated tab-local rather than shared with File
/// Explorer's <c>LinuxShellInterop</c>, the same self-contained-tab rule that has
/// <see cref="WindowsProcessInterop"/> declaring its own <c>SHObjectProperties</c>.
///
/// <see cref="TryGetIoBytes"/> reporting nothing costs the tab nothing: <c>LinuxProcessSnapshotProvider</c>
/// does not take this seam and reads disk bytes straight from <c>/proc/[pid]/io</c>. Portable managed
/// code, so no <c>[SupportedOSPlatform]</c> — there is no annotated API for CA1416 to see.
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

    /// <summary>Opens the folder holding the process's executable — no desktop offers a Properties
    /// dialog to a foreign process, so <paramref name="owner"/> has nothing to parent and is unused.</summary>
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
    /// launch so the lookup is unit-tested without starting a file manager. A kernel thread has no
    /// <c>exe</c> link and another user's is owner-only; both are ordinary, so both reveal nothing
    /// rather than failing.
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
