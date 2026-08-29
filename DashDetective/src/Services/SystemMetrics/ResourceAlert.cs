namespace DashDetective.Services.SystemMetrics;

/// <summary>Which resource an alert is about. The declaration order is the order
/// <see cref="ResourceAlertWatcher"/> picks between two metrics breaching at once.</summary>
public enum AlertMetric {
    Cpu,
    Memory,
    Gpu,
    DiskActivity,
    DiskSpace,
}

/// <summary>
/// The breach currently being reported: which resource, which device (a machine may have several GPUs,
/// disks or volumes, and saying "a GPU is busy" without saying which is not actionable), the reading that
/// tripped it, and the threshold it crossed.
/// </summary>
public sealed record ResourceAlert(AlertMetric Metric, string DeviceName, double Value, int Threshold);
