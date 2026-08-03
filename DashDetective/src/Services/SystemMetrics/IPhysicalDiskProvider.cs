using System.Collections.Generic;
using System.Threading.Tasks;

namespace DashDetective.Services.SystemMetrics;

/// <summary>
/// One physical disk: its number (<c>DeviceId</c>, matching <see cref="VolumeInfo.DiskNumber"/>), friendly
/// model, media/bus type label (e.g. "NVMe SSD"), capacity in bytes, whether it reports healthy, and its
/// temperature in °C when available (NVMe drives only; <c>null</c> otherwise). The number is the join key the
/// drive-card rollup uses to sum each disk's volumes.
/// </summary>
public readonly record struct PhysicalDiskInfo(
    int DeviceId, string Model, string TypeLabel, ulong SizeBytes, bool IsHealthy,
    double? TemperatureCelsius = null);

/// <summary>Enumerates the machine's physical disks for the Storage tab's summary cards.
/// Implementations must never throw: any failure yields an empty list.</summary>
internal interface IPhysicalDiskProvider {
    Task<IReadOnlyList<PhysicalDiskInfo>> GetAsync();
}
