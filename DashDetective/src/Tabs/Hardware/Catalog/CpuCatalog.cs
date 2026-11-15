using System;
using System.Collections.Generic;

namespace DashDetective.Tabs.Hardware.Catalog;

/// <summary>Rated CPU specs; base, L3 and socket are only a fallback for what the machine does not report.</summary>
public sealed record CpuSpec(string Boost, string Tdp, string Base, string CacheL3, string Socket);

/// <summary>
/// Bundled CPU spec table, keyed by a distinctive <b>normalized</b> model token (see
/// <see cref="HardwareCatalog.Normalize"/>) — e.g. "7600X" matches WMI's
/// "AMD Ryzen 5 7600X 6-Core Processor". Intel base is the P-core base and L3 is the whole package's.
/// </summary>
internal static class CpuCatalog {
    public static readonly IReadOnlyDictionary<string, CpuSpec> Data =
        new Dictionary<string, CpuSpec>(StringComparer.Ordinal) {
            // AMD Ryzen 7000 (Zen 4, Socket AM5).
            ["7600X"] = new("5.3 GHz", "105 W", "4.7 GHz", "32 MB", "AM5"),
            ["7700X"] = new("5.4 GHz", "105 W", "4.5 GHz", "32 MB", "AM5"),
            ["7900X"] = new("5.6 GHz", "170 W", "4.7 GHz", "64 MB", "AM5"),
            ["7950X"] = new("5.7 GHz", "170 W", "4.5 GHz", "64 MB", "AM5"),
            ["7600"] = new("5.1 GHz", "65 W", "3.8 GHz", "32 MB", "AM5"),
            ["7700"] = new("5.3 GHz", "65 W", "3.8 GHz", "32 MB", "AM5"),
            // AMD Ryzen 9000 (Zen 5, Socket AM5).
            ["9600X"] = new("5.4 GHz", "65 W", "3.9 GHz", "32 MB", "AM5"),
            ["9700X"] = new("5.5 GHz", "65 W", "3.8 GHz", "32 MB", "AM5"),
            ["9900X"] = new("5.6 GHz", "120 W", "4.4 GHz", "64 MB", "AM5"),
            ["9950X"] = new("5.7 GHz", "170 W", "4.3 GHz", "64 MB", "AM5"),
            // Intel Core 13th/14th gen (Raptor Lake, Socket LGA1700).
            ["13600K"] = new("5.1 GHz", "125 W", "3.5 GHz", "24 MB", "LGA1700"),
            ["13700K"] = new("5.4 GHz", "125 W", "3.4 GHz", "30 MB", "LGA1700"),
            ["13900K"] = new("5.8 GHz", "125 W", "3.0 GHz", "36 MB", "LGA1700"),
            ["14600K"] = new("5.3 GHz", "125 W", "3.5 GHz", "24 MB", "LGA1700"),
            ["14700K"] = new("5.6 GHz", "125 W", "3.4 GHz", "33 MB", "LGA1700"),
            ["14900K"] = new("6.0 GHz", "125 W", "3.2 GHz", "36 MB", "LGA1700"),
        };
}
