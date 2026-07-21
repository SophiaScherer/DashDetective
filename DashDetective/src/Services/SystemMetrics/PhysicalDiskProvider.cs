using DashDetective.Services.Diagnostics;
using System;
using System.Collections.Generic;
using System.Management;
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

/// <summary>
/// Enumerates every physical disk from WMI <c>MSFT_PhysicalDisk</c> (<c>root\Microsoft\Windows\Storage</c>) —
/// model, size, media/bus type and <c>HealthStatus</c> — for the Storage tab's per-disk summary cards. This
/// is the all-disks generalisation of <see cref="DiskInfoProvider"/> (which returns only the primary disk for
/// the Dashboard/Hardware). If the Storage namespace is unavailable it falls back to <c>Win32_DiskDrive</c>
/// for model + size only (health then defaults to healthy). Runs on a background thread; any failure (or a
/// non-Windows host) yields an empty list rather than throwing.
/// </summary>
public static class PhysicalDiskProvider {
    // HealthStatus 0 = Healthy; anything else (Warning/Unhealthy/Unknown) is surfaced as "Caution".
    private const int HealthStatusHealthy = 0;

    // BusType 17 = NVMe — the only bus we can read a composite temperature from (via DiskTemperatureProvider).
    private const int BusTypeNvme = 17;

    public static Task<IReadOnlyList<PhysicalDiskInfo>> GetAsync() => GetAsync(DiskTemperatureProvider.ReadCelsius);

    /// <summary>Test seam: the same read with an injected per-disk temperature reader (production passes the
    /// live NVMe reader). Keeps the WMI enumeration and the temperature read out of tests' way.</summary>
    internal static Task<IReadOnlyList<PhysicalDiskInfo>> GetAsync(Func<int, double?> readTemperatureCelsius) =>
        Task.Run(() => Read(readTemperatureCelsius));

    private static IReadOnlyList<PhysicalDiskInfo> Read(Func<int, double?> readTemperatureCelsius) {
        // Guard doubles as the platform-compatibility check for the WMI calls below.
        if (!OperatingSystem.IsWindows())
            return Array.Empty<PhysicalDiskInfo>();

        try {
            // Primary: the Storage-management namespace (model + size + media/bus type + health).
            try {
                var scope = new ManagementScope(@"\\.\root\Microsoft\Windows\Storage");
                var query = new ObjectQuery(
                    "SELECT DeviceId, FriendlyName, Size, MediaType, BusType, HealthStatus FROM MSFT_PhysicalDisk");
                using var searcher = new ManagementObjectSearcher(scope, query);
                using var results = searcher.Get();

                var disks = new List<PhysicalDiskInfo>();
                foreach (var obj in results) {
                    using (obj) {
                        int deviceId = ToInt(obj["DeviceId"]);
                        int busType = ToInt(obj["BusType"]);
                        // Only NVMe drives expose a readable composite temperature; leave others at null.
                        double? temperature = busType == BusTypeNvme ? readTemperatureCelsius(deviceId) : null;
                        disks.Add(new PhysicalDiskInfo(
                            deviceId,
                            ModelOrDefault(obj["FriendlyName"] as string),
                            DriveTypeLabel(ToInt(obj["MediaType"]), busType),
                            ToUInt64(obj["Size"]),
                            ToInt(obj["HealthStatus"]) == HealthStatusHealthy,
                            temperature));
                    }
                }

                if (disks.Count > 0)
                    return disks;
            } catch {
                // Storage namespace unavailable — fall back below.
            }

            // Fallback: classic Win32_DiskDrive (model + size only; no media/bus type or health).
            using (var searcher = new ManagementObjectSearcher("SELECT Index, Model, Size FROM Win32_DiskDrive")) {
                using var results = searcher.Get();

                var disks = new List<PhysicalDiskInfo>();
                foreach (var obj in results) {
                    using (obj) {
                        disks.Add(new PhysicalDiskInfo(
                            ToInt(obj["Index"]), ModelOrDefault(obj["Model"] as string), "",
                            ToUInt64(obj["Size"]), true));
                    }
                }

                return disks;
            }
        } catch (Exception e) {
            Log.Warn("PhysicalDiskProvider read failed", e);
            return Array.Empty<PhysicalDiskInfo>();
        }
    }

    private static string ModelOrDefault(string? model) =>
        string.IsNullOrWhiteSpace(model) ? "Drive" : model.Trim();

    /// <summary>Media/bus type label: NVMe drives read "NVMe SSD"; otherwise the media flag ("SSD"/"HDD"),
    /// or "" when unknown. BusType 17 = NVMe; MediaType 4 = SSD, 3 = HDD (same codes as
    /// <see cref="DiskInfoProvider"/> / <c>HardwareInfoProvider</c>).</summary>
    private static string DriveTypeLabel(int mediaType, int busType) {
        if (busType == BusTypeNvme)
            return "NVMe SSD";
        if (mediaType == 4)
            return "SSD";
        if (mediaType == 3)
            return "HDD";
        return "";
    }

    private static int ToInt(object? value) {
        try {
            return value is null ? 0 : Convert.ToInt32(value);
        } catch {
            return 0;
        }
    }

    private static ulong ToUInt64(object? value) {
        try {
            return value is null ? 0 : Convert.ToUInt64(value);
        } catch {
            return 0;
        }
    }
}
