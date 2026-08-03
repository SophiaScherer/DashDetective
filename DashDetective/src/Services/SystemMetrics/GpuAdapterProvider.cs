using System.Collections.Generic;
using System.Threading.Tasks;

namespace DashDetective.Services.SystemMetrics;

/// <summary>
/// TEMPORARY façade over <see cref="IGpuAdapterProvider"/>, kept for exactly one commit so the provider
/// bodies could move behind the seam without touching any call site. The next phase routes
/// <c>DeviceInventory</c> through the injected bundle and deletes this file.
/// </summary>
public static class GpuAdapterProvider {
    public static Task<IReadOnlyList<GpuAdapter>> GetAsync() =>
        HardwareProviders.ForCurrentPlatform().GpuAdapters.GetAsync();
}
