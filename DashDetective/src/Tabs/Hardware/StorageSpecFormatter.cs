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

    // MSFT_PhysicalDisk codes: BusType 17 = NVMe; MediaType 4 = SSD, 3 = HDD.
    private const int BusTypeNvme = 17;
    private const int MediaTypeSsd = 4;
    private const int MediaTypeHdd = 3;

    // MSFT_PhysicalDisk HealthStatus codes.
    private const int HealthHealthy = 0;
    private const int HealthWarning = 1;
    private const int HealthUnhealthy = 2;

    /// <summary>The card subtitle: drive count plus combined capacity ("3 drives · 4 TB total").</summary>
    public static string Summary(int driveCount, ulong totalBytes) {
        var noun = driveCount == 1 ? "drive" : "drives";
        return $"{driveCount} {noun} · {Capacity(totalBytes)} total";
    }

    /// <summary>Media/bus type label for a physical disk: NVMe wins over the SSD/HDD media flag; "" if
    /// neither is known (the row then shows size only).</summary>
    public static string TypeLabel(int mediaType, int busType) {
        if (busType == BusTypeNvme) return "NVMe";
        if (mediaType == MediaTypeSsd) return "SSD";
        if (mediaType == MediaTypeHdd) return "HDD";
        return "";
    }

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
