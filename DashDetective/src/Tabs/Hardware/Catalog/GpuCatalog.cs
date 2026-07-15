using System;
using System.Collections.Generic;

namespace DashDetective.Tabs.Hardware.Catalog;

/// <summary>Rated GPU specs WMI can't report — memory (size + type), CUDA-core count, boost clock, bus.</summary>
public sealed record GpuSpec(string Memory, string CudaCores, string BoostClock, string Bus);

/// <summary>
/// Bundled GPU spec table, keyed by a distinctive <b>normalized</b> model token (see
/// <see cref="HardwareCatalog.Normalize"/>) — e.g. "RTX 3060" matches WMI's "NVIDIA GeForce RTX 3060".
/// Add a part by adding one line. Data is seeded in the Graphics phase; an empty table leaves the
/// fields as "—".
/// </summary>
internal static class GpuCatalog {
    public static readonly IReadOnlyDictionary<string, GpuSpec> Data =
        new Dictionary<string, GpuSpec>(StringComparer.Ordinal) {
            // Populated in Phase 9.
        };
}
