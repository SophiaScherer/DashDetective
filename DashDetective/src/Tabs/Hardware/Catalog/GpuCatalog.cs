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
            // NVIDIA GeForce RTX 30-series (Founders reference boost clocks).
            ["RTX 3060 TI"] = new("8 GB GDDR6", "4,864", "1.67 GHz", "PCIe 4.0 x16"),
            ["RTX 3060"] = new("12 GB GDDR6", "3,584", "1.78 GHz", "PCIe 4.0 x16"),
            ["RTX 3070 TI"] = new("8 GB GDDR6X", "6,144", "1.77 GHz", "PCIe 4.0 x16"),
            ["RTX 3070"] = new("8 GB GDDR6", "5,888", "1.73 GHz", "PCIe 4.0 x16"),
            ["RTX 3080 TI"] = new("12 GB GDDR6X", "10,240", "1.67 GHz", "PCIe 4.0 x16"),
            ["RTX 3080"] = new("10 GB GDDR6X", "8,704", "1.71 GHz", "PCIe 4.0 x16"),
            ["RTX 3090 TI"] = new("24 GB GDDR6X", "10,752", "1.86 GHz", "PCIe 4.0 x16"),
            ["RTX 3090"] = new("24 GB GDDR6X", "10,496", "1.70 GHz", "PCIe 4.0 x16"),
            // NVIDIA GeForce RTX 40-series.
            ["RTX 4060 TI"] = new("8 GB GDDR6", "4,352", "2.54 GHz", "PCIe 4.0 x8"),
            ["RTX 4060"] = new("8 GB GDDR6", "3,072", "2.46 GHz", "PCIe 4.0 x8"),
            ["RTX 4070 TI SUPER"] = new("16 GB GDDR6X", "8,448", "2.61 GHz", "PCIe 4.0 x16"),
            ["RTX 4070 TI"] = new("12 GB GDDR6X", "7,680", "2.61 GHz", "PCIe 4.0 x16"),
            ["RTX 4070 SUPER"] = new("12 GB GDDR6X", "7,168", "2.48 GHz", "PCIe 4.0 x16"),
            ["RTX 4070"] = new("12 GB GDDR6X", "5,888", "2.48 GHz", "PCIe 4.0 x16"),
            ["RTX 4080 SUPER"] = new("16 GB GDDR6X", "10,240", "2.55 GHz", "PCIe 4.0 x16"),
            ["RTX 4080"] = new("16 GB GDDR6X", "9,728", "2.51 GHz", "PCIe 4.0 x16"),
            ["RTX 4090"] = new("24 GB GDDR6X", "16,384", "2.52 GHz", "PCIe 4.0 x16"),
            // AMD Radeon (no CUDA cores — that row is N/A → "—").
            ["RX 7900 XTX"] = new("24 GB GDDR6", "—", "2.50 GHz", "PCIe 4.0 x16"),
            ["RX 7800 XT"] = new("16 GB GDDR6", "—", "2.43 GHz", "PCIe 4.0 x16"),
            ["RX 7700 XT"] = new("12 GB GDDR6", "—", "2.54 GHz", "PCIe 4.0 x16"),
            ["RX 6700 XT"] = new("12 GB GDDR6", "—", "2.58 GHz", "PCIe 4.0 x16"),
        };
}
