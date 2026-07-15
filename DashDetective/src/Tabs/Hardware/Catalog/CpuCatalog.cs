using System;
using System.Collections.Generic;

namespace DashDetective.Tabs.Hardware.Catalog;

/// <summary>Rated CPU specs WMI can't report — boost clock and TDP.</summary>
public sealed record CpuSpec(string Boost, string Tdp);

/// <summary>
/// Bundled CPU spec table, keyed by a distinctive <b>normalized</b> model token (see
/// <see cref="HardwareCatalog.Normalize"/>) — e.g. "7600X" matches WMI's
/// "AMD Ryzen 5 7600X 6-Core Processor". Add a part by adding one line. Data is seeded in the Processor
/// phase; an empty table simply leaves the fields as "—".
/// </summary>
internal static class CpuCatalog {
    public static readonly IReadOnlyDictionary<string, CpuSpec> Data =
        new Dictionary<string, CpuSpec>(StringComparer.Ordinal) {
            // AMD Ryzen (AM5). Boost = max single-core; TDP = rated default.
            ["7600X"] = new("5.3 GHz", "105 W"),
            ["7700X"] = new("5.4 GHz", "105 W"),
            ["7900X"] = new("5.6 GHz", "170 W"),
            ["7950X"] = new("5.7 GHz", "170 W"),
            ["7600"] = new("5.1 GHz", "65 W"),
            ["7700"] = new("5.3 GHz", "65 W"),
            ["9600X"] = new("5.4 GHz", "65 W"),
            ["9700X"] = new("5.5 GHz", "65 W"),
            ["9900X"] = new("5.6 GHz", "120 W"),
            ["9950X"] = new("5.7 GHz", "170 W"),
            // Intel Core (13th/14th gen). TDP = Processor Base Power.
            ["13600K"] = new("5.1 GHz", "125 W"),
            ["13700K"] = new("5.4 GHz", "125 W"),
            ["13900K"] = new("5.8 GHz", "125 W"),
            ["14600K"] = new("5.3 GHz", "125 W"),
            ["14700K"] = new("5.6 GHz", "125 W"),
            ["14900K"] = new("6.0 GHz", "125 W"),
        };
}
