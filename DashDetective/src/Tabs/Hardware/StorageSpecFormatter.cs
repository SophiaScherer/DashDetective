using DashDetective.Services.SystemMetrics;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace DashDetective.Tabs.Hardware;

/// <summary>
/// Formats the Storage Devices card's subtitle and per-drive rows. Capacities use decimal (marketing)
/// units so a drive sold as 2 TB reads "2 TB" — deliberately unlike <c>FileSizeFormatter</c>, which is
/// binary because a file's size is. Always InvariantCulture, matching the app's convention.
/// </summary>
internal static class StorageSpecFormatter {
    private const double Tb = 1_000_000_000_000d;
    private const double Gb = 1_000_000_000d;

    // MSFT_PhysicalDisk HealthStatus codes.
    private const int HealthHealthy = 0;
    private const int HealthWarning = 1;
    private const int HealthUnhealthy = 2;

    /// <summary>The card subtitle: drive count plus combined capacity ("3 drives · 4 TB total").</summary>
    public static string Summary(int driveCount, ulong totalBytes) {
        var noun = driveCount == 1 ? "drive" : "drives";
        return $"{driveCount} {noun} · {Capacity(totalBytes)} total";
    }

    /// <summary>The spec row's drive type — terser than the Storage tab's wording, since it sits beside a
    /// capacity on one line. "" when the drive's kind is unknown (the row then shows size only).</summary>
    public static string TypeLabel(DriveKind kind) => kind switch {
        DriveKind.Nvme => "NVMe",
        DriveKind.Ssd => "SSD",
        DriveKind.Hdd => "HDD",
        _ => "",
    };

    /// <summary>The same label from WMI's raw code pair, decoded once in <see cref="DriveKinds"/>.</summary>
    public static string TypeLabel(int mediaType, int busType) =>
        TypeLabel(DriveKinds.FromStorageCodes(mediaType, busType));

    /// <summary>A drive row's value: capacity plus optional type, e.g. "2 TB NVMe" or "500 GB".</summary>
    public static string DriveDetail(ulong bytes, string type) {
        if (bytes == 0)
            return string.IsNullOrEmpty(type) ? "—" : type;
        var size = Capacity(bytes);
        return string.IsNullOrEmpty(type) ? size : $"{size} {type}";
    }

    /// <summary>Formats drive capacity in decimal units — TB at/above 1 TB, else GB — dropping a
    /// trailing ".0" (2000398934016 → "2 TB").</summary>
    public static string Capacity(ulong bytes) =>
        bytes >= Tb
            ? (bytes / Tb).ToString("0.#", CultureInfo.InvariantCulture) + " TB"
            : (bytes / Gb).ToString("0.#", CultureInfo.InvariantCulture) + " GB";

    /// <summary>Worst-status-wins summary of the drives' HealthStatus codes; an unrecognised code
    /// yields "—" rather than overstating health.</summary>
    public static string Health(IReadOnlyList<int> codes) {
        if (codes.Count == 0) return "—";
        if (codes.Contains(HealthUnhealthy)) return "Unhealthy";
        if (codes.Contains(HealthWarning)) return "Warning";
        if (codes.All(c => c == HealthHealthy)) return "Good";
        return "—";
    }
}
