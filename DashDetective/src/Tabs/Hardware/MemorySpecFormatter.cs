using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace DashDetective.Tabs.Hardware;

/// <summary>
/// Formats the Memory card's subtitle and spec rows from the per-module WMI facts. Capacities are binary
/// GB and drop a trailing ".0"; always InvariantCulture, matching the app's convention. A figure WMI
/// cannot supply yields the neutral "—".
/// </summary>
internal static class MemorySpecFormatter {
    /// <summary>Formats a GB figure without a trailing ".0" for whole values (16.0 → "16", 1.5 → "1.5").</summary>
    public static string Gb(double gb) =>
        gb.ToString(gb % 1 == 0 ? "0" : "0.#", CultureInfo.InvariantCulture);

    /// <summary>The card subtitle: total capacity, type and — when known — the running speed
    /// ("32 GB DDR5-6000", or "32 GB DDR5" when the speed is unavailable).</summary>
    public static string Summary(double totalGb, string type, int speedMts) =>
        speedMts > 0 ? $"{Gb(totalGb)} GB {type}-{speedMts}" : $"{Gb(totalGb)} GB {type}";

    /// <summary>Renders the module layout: "2 × 16 GB" when uniform, else "16 GB + 8 GB".</summary>
    public static string Modules(IReadOnlyList<double> moduleGbs) {
        if (moduleGbs.Count == 0)
            return "—";
        if (moduleGbs.Distinct().Count() == 1)
            return $"{moduleGbs.Count} × {Gb(moduleGbs[0])} GB";
        return string.Join(" + ", moduleGbs.Select(g => $"{Gb(g)} GB"));
    }

    /// <summary>Formats the running transfer rate ("6000 MT/s"), or "—" when unavailable.</summary>
    public static string Speed(int megatransfersPerSecond) =>
        megatransfersPerSecond > 0 ? $"{megatransfersPerSecond} MT/s" : "—";

    /// <summary>Populated over total DIMM slots ("2 / 4"). Falls back to the populated count alone when
    /// the board's slot total is unavailable.</summary>
    public static string SlotsUsed(int populated, int totalSlots) =>
        totalSlots > 0 ? $"{populated} / {totalSlots}" : populated.ToString(CultureInfo.InvariantCulture);

    /// <summary>Formats the configured voltage, which WMI reports in millivolts, as volts ("1.35 V").</summary>
    public static string Voltage(int millivolts) =>
        millivolts > 0
            ? (millivolts / 1000.0).ToString("0.##", CultureInfo.InvariantCulture) + " V"
            : "—";

    /// <summary>Maps an SMBIOS memory-type code to a human label, falling back to "RAM".</summary>
    public static string TypeLabel(int smbiosType) => smbiosType switch {
        20 => "DDR",
        21 => "DDR2",
        24 => "DDR3",
        26 => "DDR4",
        34 => "DDR5",
        27 => "LPDDR",
        28 => "LPDDR2",
        29 => "LPDDR3",
        30 => "LPDDR4",
        35 => "LPDDR5",
        _ => "RAM",
    };
}
