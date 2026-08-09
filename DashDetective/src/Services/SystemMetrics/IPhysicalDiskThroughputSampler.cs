using System;
using System.Collections.Generic;

namespace DashDetective.Services.SystemMetrics;

/// <summary>Per-disk snapshot, keyed by disk number: read/write throughput (bytes per second), Task Manager's
/// disk "Active time" as a percentage (0–100, <c>100 − % Idle Time</c>), the average transfer response
/// time in seconds, and the average disk queue length (outstanding requests).</summary>
public readonly record struct DiskThroughputSample(
    int DiskNumber, double ReadBytesPerSec, double WriteBytesPerSec, double ActivePercent, double ResponseSeconds,
    double QueueLength);

/// <summary>
/// A source of per-physical-disk activity, one reading per disk. Drives the Storage tab's per-drive
/// Read/Write readouts and Disk Activity panel, the Dashboard's disk cards and the Performance tab's disk
/// view — each page owns its own instance and its own timer.
///
/// Unlike the <c>HardwareProviders</c> members this is <b>stateful</b>: every implementation reports the
/// interval since the previous call, so an instance may not be shared between pages. Implementations must
/// never throw: any failure yields an empty set, and an implementation that has gone inert yields one
/// forever.
/// </summary>
internal interface IPhysicalDiskThroughputSampler : IDisposable {
    /// <summary>Returns one reading per physical disk for the interval since the previous call. Empty on
    /// any failure.</summary>
    IReadOnlyList<DiskThroughputSample> Sample();

    /// <summary>The reader for this machine — the only place the platform is decided for this seam.</summary>
    static IPhysicalDiskThroughputSampler ForCurrentPlatform() {
        if (OperatingSystem.IsWindows())
            return new WindowsPhysicalDiskThroughputSampler();

        return new UnsupportedPhysicalDiskThroughputSampler();
    }
}
