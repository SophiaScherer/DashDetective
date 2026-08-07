using System;

namespace DashDetective.Services.SystemMetrics;

/// <summary>
/// A single physical-memory snapshot: load as a percentage (0–100), used and total physical bytes,
/// plus the system commit charge and limit (<c>CommittedBytes</c> of <c>CommitLimitBytes</c>) — Task
/// Manager's "Committed" figure, which counts pagefile-backed virtual memory beyond physical RAM.
/// </summary>
public readonly record struct MemorySample(
    double LoadPercent, ulong UsedBytes, ulong TotalBytes,
    ulong CommittedBytes, ulong CommitLimitBytes);

/// <summary>
/// A source of system-wide physical-memory usage, driving the shared Memory metric feed (Dashboard,
/// Performance and Processes all read it). Memory is an absolute reading, so unlike the CPU seam there is
/// no prior state to diff — implementations are stateless.
///
/// Implementations must never throw: any failure yields a zeroed <see cref="MemorySample"/>, which the
/// subscribing pages render as empty tiles rather than failing the page.
/// </summary>
internal interface IMemoryUsageSampler {
    /// <summary>Returns the current physical-memory snapshot at the moment of the call, or a zeroed
    /// sample when no reading is available.</summary>
    MemorySample Sample();

    /// <summary>The reader for this machine — the only place the platform is decided for this seam.</summary>
    static IMemoryUsageSampler ForCurrentPlatform() {
        if (OperatingSystem.IsWindows())
            return new WindowsMemoryUsageSampler();

        if (OperatingSystem.IsLinux())
            return new LinuxMemoryUsageSampler();

        return new UnsupportedMemoryUsageSampler();
    }
}
