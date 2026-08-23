using System;

namespace DashDetective.Tabs.Settings;

/// <summary>
/// Whether reading NVIDIA GPU utilization costs a helper process on this machine — which is what the
/// Settings toggle has to tell the user, and its only consumer. Linux only: there the
/// figure exists solely through <c>nvidia-smi</c>, which is why it is opt-in at all. Windows reads the
/// same number from a performance counter it is already polling, so the setting has nothing to turn on
/// there and its toggle is inert — see <c>IGpuUsageSampler.NvidiaMetricsEnabled</c>, whose default
/// implementation discards the write.
/// </summary>
internal static class GpuMetricsSupport {
    internal static bool NeedsHelperTool { get; } = OperatingSystem.IsLinux();
}
