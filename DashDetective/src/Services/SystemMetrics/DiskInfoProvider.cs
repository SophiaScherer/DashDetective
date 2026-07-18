using System;
using System.Management;
using System.Threading.Tasks;

namespace DashDetective.Services.SystemMetrics;

/// <summary>
/// Static facts about the primary physical disk (the lowest DeviceId — normally the boot drive): its
/// model, media/bus type label (e.g. "NVMe SSD") and capacity in GB. <see cref="Unknown"/> is the
/// fallback when nothing can be read.
/// </summary>
public readonly record struct DiskStaticInfo(string Model, string TypeLabel, double SizeGb) {
    public static DiskStaticInfo Unknown { get; } = new("Unknown drive", "", 0);
}

/// <summary>
/// Reads static disk hardware information from WMI. Primary source is <c>MSFT_PhysicalDisk</c>
/// (<c>root\Microsoft\Windows\Storage</c>), which gives the friendly model, size and media/bus type in
/// one place; if that namespace is unavailable it falls back to <c>Win32_DiskDrive</c> for model + size
/// only. The query is comparatively slow and blocking, so it runs on a background thread and is awaited
/// once at startup. Any failure (or a non-Windows host) yields <see cref="DiskStaticInfo.Unknown"/>
/// rather than throwing. Mirrors <c>HardwareInfoProvider</c>'s drive reading, scoped to one disk.
/// </summary>
public static class DiskInfoProvider {
    public static Task<DiskStaticInfo> GetAsync() => Task.Run(Read);

    private static DiskStaticInfo Read() {
        // Guard doubles as the platform-compatibility check for the WMI calls below.
        if (!OperatingSystem.IsWindows())
            return DiskStaticInfo.Unknown;

        try {
            // Primary: the Storage-management namespace (model + size + media/bus type).
            try {
                var scope = new ManagementScope(@"\\.\root\Microsoft\Windows\Storage");
                var query = new ObjectQuery(
                    "SELECT DeviceId, FriendlyName, Size, MediaType, BusType FROM MSFT_PhysicalDisk");
                using var searcher = new ManagementObjectSearcher(scope, query);
                using var results = searcher.Get();

                DiskStaticInfo? best = null;
                var bestId = int.MaxValue;
                foreach (var obj in results) {
                    using (obj) {
                        var id = ToInt(obj["DeviceId"]);
                        if (id > bestId)
                            continue;
                        bestId = id;
                        best = new DiskStaticInfo(
                            ModelOrDefault(obj["FriendlyName"] as string),
                            DriveTypeLabel(ToInt(obj["MediaType"]), ToInt(obj["BusType"])),
                            ToUInt64(obj["Size"]) / (double)(1L << 30));
                    }
                }

                if (best is { } disk)
                    return disk;
            } catch {
                // Storage namespace unavailable — fall back below.
            }

            // Fallback: classic Win32_DiskDrive (model + size only, no media/bus type).
            using (var searcher = new ManagementObjectSearcher("SELECT Index, Model, Size FROM Win32_DiskDrive")) {
                using var results = searcher.Get();

                DiskStaticInfo? best = null;
                var bestIndex = int.MaxValue;
                foreach (var obj in results) {
                    using (obj) {
                        var index = ToInt(obj["Index"]);
                        if (index > bestIndex)
                            continue;
                        bestIndex = index;
                        best = new DiskStaticInfo(
                            ModelOrDefault(obj["Model"] as string), "",
                            ToUInt64(obj["Size"]) / (double)(1L << 30));
                    }
                }

                if (best is { } disk)
                    return disk;
            }

            return DiskStaticInfo.Unknown;
        } catch {
            return DiskStaticInfo.Unknown;
        }
    }

    private static string ModelOrDefault(string? model) =>
        string.IsNullOrWhiteSpace(model) ? "Drive" : model.Trim();

    /// <summary>Media/bus type label for a physical disk: NVMe drives are SSDs, so they read
    /// "NVMe SSD"; otherwise the media flag ("SSD"/"HDD"), or "" when unknown. BusType 17 = NVMe;
    /// MediaType 4 = SSD, 3 = HDD (same codes as <c>HardwareInfoProvider</c>).</summary>
    private static string DriveTypeLabel(int mediaType, int busType) {
        if (busType == 17)
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
