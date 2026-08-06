using System;

namespace DashDetective.Services.SystemMetrics;

/// <summary>
/// One system-wide counters snapshot: the file-cache size in bytes plus the live process, thread and
/// handle totals — the figures Task Manager shows on its CPU and Memory panes. Every member is nullable
/// because a platform may genuinely have no analogue for one of them, which the tiles render as "—"
/// rather than as a zero.
/// </summary>
public readonly record struct SystemPerformanceSample(
    ulong? CachedBytes, int? ProcessCount, int? ThreadCount, int? HandleCount);

/// <summary>
/// A source of the system-wide counters the Performance tab's CPU and Memory panes share. Read on the
/// sampling tick, so implementations must be cheap and must never throw — any failure yields
/// <c>null</c>, or a sample whose unavailable members are <c>null</c>.
/// </summary>
internal interface ISystemPerformanceProvider {
    /// <summary>The current system counters, or <c>null</c> when nothing is readable.</summary>
    SystemPerformanceSample? Read();

    /// <summary>The reader for this machine — the only place the platform is decided for this seam.</summary>
    static ISystemPerformanceProvider ForCurrentPlatform() {
        if (OperatingSystem.IsWindows())
            return new WindowsSystemPerformanceProvider();

        return new UnsupportedSystemPerformanceProvider();
    }
}
