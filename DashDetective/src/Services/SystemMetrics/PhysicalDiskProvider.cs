using System.Collections.Generic;
using System.Threading.Tasks;

namespace DashDetective.Services.SystemMetrics;

/// <summary>
/// TEMPORARY façade over <see cref="IPhysicalDiskProvider"/>, kept for exactly one commit so the provider
/// bodies could move behind the seam without touching any call site. The next phase routes
/// <c>DeviceInventory</c>, <c>DashboardViewModel</c> and <c>StorageViewModel</c> through the injected
/// bundle and deletes this file.
/// </summary>
public static class PhysicalDiskProvider {
    public static Task<IReadOnlyList<PhysicalDiskInfo>> GetAsync() =>
        HardwareProviders.ForCurrentPlatform().Disks.GetAsync();
}
