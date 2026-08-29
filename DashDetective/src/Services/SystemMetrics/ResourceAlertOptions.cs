namespace DashDetective.Services.SystemMetrics;

/// <summary>
/// The user's alert thresholds, as percentages. <b>Zero means the metric is not watched</b> — collapsing
/// "enabled" into the value keeps one uniform control per row and one uniform check in the watcher.
///
/// The four usage thresholds are an upper bound (alert at or above); <see cref="LowDiskFreePercent"/> is
/// a lower one (alert at or below), because the actionable end of free space is the small end.
/// </summary>
public sealed record ResourceAlertOptions {
    public int CpuPercent { get; init; } = 90;
    public int MemoryPercent { get; init; } = 90;

    /// <summary>Off by default, unlike CPU and memory: a GPU pinned at 100% for ten seconds is what
    /// gaming, rendering and model inference all look like, so watching it by default would mostly report
    /// the machine doing its job.</summary>
    public int GpuPercent { get; init; }

    /// <summary>Off by default, for the same reason as <see cref="GpuPercent"/> — a large copy or an
    /// update holds disk active time at 100% legitimately.</summary>
    public int DiskActivePercent { get; init; }

    /// <summary>Alert when any volume falls to or below this much free space. On by default: unlike the
    /// usage metrics it has almost no false positives, and it is the one alert here that needs acting on
    /// rather than waiting out.</summary>
    public int LowDiskFreePercent { get; init; } = 10;

    /// <summary>How long a usage metric must stay over its threshold before it counts. Seconds rather
    /// than samples, so the wait means the same thing at every refresh interval.</summary>
    public int SustainSeconds { get; init; } = 10;

    /// <summary>The baseline: the CPU and memory behaviour the app shipped with, plus low-disk-space.</summary>
    public static ResourceAlertOptions Defaults { get; } = new();
}
