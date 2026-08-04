using DashDetective.Services.Diagnostics;
using System;
using System.Collections.Generic;
using System.Management;
using System.Runtime.Versioning;
using System.Threading.Tasks;

namespace DashDetective.Services.SystemMetrics;


/// <summary>
/// Enumerates every physical disk from WMI <c>MSFT_PhysicalDisk</c> (<c>root\Microsoft\Windows\Storage</c>) —
/// model, size, media/bus type and <c>HealthStatus</c> — for the Storage tab's per-disk summary cards. If
/// the Storage namespace is unavailable it falls back to <c>Win32_DiskDrive</c>
/// for model + size only (health then defaults to healthy). Runs on a background thread; any failure (or a
/// non-Windows host) yields an empty list rather than throwing.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsPhysicalDiskProvider(IDiskTemperatureProvider temperature) : IPhysicalDiskProvider {
    // HealthStatus 0 = Healthy; anything else (Warning/Unhealthy/Unknown) is surfaced as "Caution".
    private const int HealthStatusHealthy = 0;

    public Task<IReadOnlyList<PhysicalDiskInfo>> GetAsync() => Task.Run(Read);

    private IReadOnlyList<PhysicalDiskInfo> Read() {
        var readTemperatureCelsius = temperature.ReadCelsius;

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
                        var kind = DriveKinds.FromStorageCodes(ToInt(obj["MediaType"]), ToInt(obj["BusType"]));
                        // Only NVMe drives expose a readable composite temperature; leave others at null.
                        double? temperature = kind == DriveKind.Nvme ? readTemperatureCelsius(deviceId) : null;
                        disks.Add(new PhysicalDiskInfo(
                            deviceId,
                            ModelOrDefault(obj["FriendlyName"] as string),
                            DriveTypeLabel(kind),
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

    /// <summary>The drive card's type label — spelled out ("NVMe SSD") since it heads its own card, unlike
    /// the Hardware tab's terser spec row. "" when the kind is unknown. The codes behind
    /// <paramref name="kind"/> are decoded once, in <see cref="DriveKinds"/>.</summary>
    private static string DriveTypeLabel(DriveKind kind) => kind switch {
        DriveKind.Nvme => "NVMe SSD",
        DriveKind.Ssd => "SSD",
        DriveKind.Hdd => "HDD",
        _ => "",
    };

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

/// <summary>The no-disks set — what the old <c>OperatingSystem.IsWindows()</c> guard returned.</summary>
internal sealed class UnsupportedPhysicalDiskProvider : IPhysicalDiskProvider {
    public Task<IReadOnlyList<PhysicalDiskInfo>> GetAsync() =>
        Task.FromResult<IReadOnlyList<PhysicalDiskInfo>>([]);
}
