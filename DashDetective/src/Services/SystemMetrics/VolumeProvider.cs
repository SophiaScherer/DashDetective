using System.Collections.Generic;
using System.Threading.Tasks;

namespace DashDetective.Services.SystemMetrics;

/// <summary>
/// TEMPORARY façade over <see cref="IVolumeProvider"/>, kept for exactly one commit so the provider
/// bodies could move behind the seam without touching any call site. The next phase routes the consuming
/// view models through the injected bundle and deletes this file.
/// </summary>
public static class VolumeProvider {
    public static Task<IReadOnlyList<VolumeInfo>> GetAsync() =>
        HardwareProviders.ForCurrentPlatform().Volumes.GetAsync();
}
