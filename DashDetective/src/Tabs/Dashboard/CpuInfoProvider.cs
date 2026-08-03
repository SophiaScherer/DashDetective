using System.Threading.Tasks;

namespace DashDetective.Tabs.Dashboard;

/// <summary>
/// TEMPORARY façade over <see cref="ICpuInfoProvider"/>, kept for exactly one commit so the provider
/// bodies could move behind the seam without touching any call site. The next phase routes
/// <c>DashboardViewModel</c> / <c>PerformanceViewModel</c> / <c>DeviceInventory</c> through the injected
/// bundle and deletes this file.
/// </summary>
public static class CpuInfoProvider {
    public static Task<CpuStaticInfo> GetAsync() =>
        Services.SystemMetrics.HardwareProviders.ForCurrentPlatform().Cpu.GetAsync();
}
