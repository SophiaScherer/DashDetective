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
            // Populated in Phase 8.
        };
}
