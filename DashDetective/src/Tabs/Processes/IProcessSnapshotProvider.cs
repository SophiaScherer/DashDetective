using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DashDetective.Tabs.Processes;

/// <summary>
/// Snapshots the running processes for the process table. Implementations must never throw: any failure
/// yields an empty list, and each per-process field falls back independently.
///
/// Stateful by nature — CPU% and disk rate are differences across consecutive snapshots — so an
/// implementation is single-consumer and must not be shared between pages.
/// </summary>
internal interface IProcessSnapshotProvider {
    Task<IReadOnlyList<ProcessInfo>> GetAsync(CancellationToken token = default);

    /// <summary>The snapshot reader for this machine, or one that reports no processes. The Linux arm takes
    /// no <paramref name="interop"/>: that seam is Windows-shaped (its I/O counters are read from a managed
    /// <c>Process</c> handle), and <c>/proc/[pid]/io</c> supplies the same figure directly.</summary>
    static IProcessSnapshotProvider ForCurrentPlatform(IProcessInterop interop) =>
        OperatingSystem.IsWindows() ? new WindowsProcessSnapshotProvider(interop)
        : OperatingSystem.IsLinux() ? new LinuxProcessSnapshotProvider()
        : new UnsupportedProcessSnapshotProvider();
}
