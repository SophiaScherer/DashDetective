using System.Threading.Tasks;

namespace DashDetective.Tabs.Dashboard;

/// <summary>
/// TEMPORARY façade over <see cref="ISystemInfoProvider"/>, kept for exactly one commit so the provider
/// bodies could move behind the seam without touching any call site. The next phase routes the
/// consuming view models through the injected bundle and deletes this file.
/// </summary>
public static class SystemInfoProvider {
    public static Task<SystemStaticInfo> GetAsync() =>
        Services.SystemMetrics.HardwareProviders.ForCurrentPlatform().System.GetAsync();
}
