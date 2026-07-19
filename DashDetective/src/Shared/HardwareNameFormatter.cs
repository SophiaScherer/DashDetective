using System;
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
            return "Unknown CPU";

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
            return "Unknown GPU";

        var name = raw.Replace("(R)", "").Replace("(r)", "")
                      .Replace("(TM)", "").Replace("(tm)", "");

        foreach (var vendor in new[] { "NVIDIA ", "AMD ", "Intel " })
            if (name.StartsWith(vendor, StringComparison.OrdinalIgnoreCase))
                name = name[vendor.Length..];

        return WhitespaceRegex().Replace(name, " ").Trim();
    }

    [GeneratedRegex(@"\s+\d+-Core Processor")]
    private static partial Regex CoreProcessorRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
