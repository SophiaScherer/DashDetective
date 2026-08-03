using System;
using System.Collections.Generic;
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
    Task<IReadOnlyList<ProcessInfo>> GetAsync();

    /// <summary>The snapshot reader for this machine, or one that reports no processes.</summary>
    static IProcessSnapshotProvider ForCurrentPlatform(IProcessInterop interop) =>
        OperatingSystem.IsWindows()
            ? new WindowsProcessSnapshotProvider(interop)
            : new UnsupportedProcessSnapshotProvider();
}
