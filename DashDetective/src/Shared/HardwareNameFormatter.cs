using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace DashDetective.Shared;

/// <summary>
/// Trims vendor/marketing decoration from raw CPU and GPU names so they fit the compact card captions
/// (e.g. "AMD Ryzen 5 7600X 6-Core Processor" → "AMD Ryzen 5 7600X"). Shared by the Dashboard and
/// Performance tabs.
/// </summary>
/// <remarks>
/// Distinct from <c>HardwareCatalog.Normalize</c>: this is a display trim (what the user reads),
/// whereas Normalize produces a lookup key for the hardware spec tables. They are intentionally not
/// merged.
/// </remarks>
public static partial class HardwareNameFormatter {
    /// <summary>Trims "(R)", "(TM)", "N-Core Processor" and a trailing "CPU @ …GHz" from a processor
    /// name, e.g. "AMD Ryzen 5 7600X 6-Core Processor" → "AMD Ryzen 5 7600X".</summary>
    public static string ShortenCpu(string raw) {
        if (string.IsNullOrWhiteSpace(raw))
            return Placeholders.UnknownCpu;

        var name = raw.Replace("(R)", "").Replace("(r)", "")
                      .Replace("(TM)", "").Replace("(tm)", "");

        var atIndex = name.IndexOf(" @", StringComparison.Ordinal);
        if (atIndex >= 0)
            name = name[..atIndex];

        name = CoreProcessorRegex().Replace(name, "");
        name = name.Replace(" Processor", "").Replace(" CPU", "");
        return WhitespaceRegex().Replace(name, " ").Trim();
    }

    /// <summary>Trims vendor prefixes ("NVIDIA", "AMD", "Intel") and "(R)"/"(TM)" from an adapter name,
    /// e.g. "NVIDIA GeForce RTX 3060" → "GeForce RTX 3060".</summary>
    public static string ShortenGpu(string raw) {
        if (string.IsNullOrWhiteSpace(raw))
            return Placeholders.UnknownGpu;

        var name = raw.Replace("(R)", "").Replace("(r)", "")
                      .Replace("(TM)", "").Replace("(tm)", "");

        foreach (var vendor in new[] { "NVIDIA ", "AMD ", "Intel " })
            if (name.StartsWith(vendor, StringComparison.OrdinalIgnoreCase))
                name = name[vendor.Length..];

        return WhitespaceRegex().Replace(name, " ").Trim();
    }

    /// <summary>
    /// The processor's caption line, e.g. "6 cores · 4.7 GHz" — physical cores where known, falling back
    /// to logical, and the clock only when there is one. Empty when neither is known, so the caller can
    /// choose its own placeholder.
    /// </summary>
    /// <remarks>
    /// Takes primitives rather than <c>CpuStaticInfo</c> so <c>src/Shared</c> keeps its independence
    /// from any tab's model. The Performance rail and <c>DeviceInventory</c> had byte-identical copies.
    /// </remarks>
    public static string CoreSummary(int physicalCores, int logicalCores, double maxClockMhz) {
        var cores = physicalCores > 0 ? physicalCores : logicalCores;
        if (cores > 0 && maxClockMhz > 0)
            return $"{cores} cores · {(maxClockMhz / 1000.0).ToString("0.0", CultureInfo.InvariantCulture)} GHz";
        return cores > 0 ? $"{cores} cores" : "";
    }

    /// <summary>The memory caption, e.g. "DDR5-4800 · 2 slots" — the speed and slot count each appear
    /// only when known. See <see cref="CoreSummary"/> on why this takes primitives.</summary>
    public static string MemorySummary(string typeLabel, int speedMhz, int moduleCount) {
        var label = speedMhz > 0
            ? $"{typeLabel}-{speedMhz.ToString(CultureInfo.InvariantCulture)}"
            : typeLabel;
        return moduleCount > 0
            ? $"{label} · {moduleCount.ToString(CultureInfo.InvariantCulture)} slots"
            : label;
    }

    [GeneratedRegex(@"\s+\d+-Core Processor")]
    private static partial Regex CoreProcessorRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
