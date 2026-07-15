using System;
using System.Collections.Generic;

namespace DashDetective.Tabs.Hardware.Catalog;

/// <summary>Rated memory spec WMI can't report — the module's timings (rated/XMP profile).</summary>
public sealed record MemorySpec(string Timings);

/// <summary>
/// Bundled memory spec table, keyed by the <b>normalized</b> module part number (see
/// <see cref="HardwareCatalog.Normalize"/>) from <c>Win32_PhysicalMemory.PartNumber</c>. Add a kit by
/// adding one line. Data is seeded in the Memory phase; an empty table leaves Timings as "—". The
/// value is the module's <i>rated</i> profile — the applied timings can differ under manual tuning.
/// </summary>
internal static class MemoryCatalog {
    public static readonly IReadOnlyDictionary<string, MemorySpec> Data =
        new Dictionary<string, MemorySpec>(StringComparer.Ordinal) {
            // Keyed by the normalized part number (hyphens → spaces). Timings are CL-tRCD-tRP-tRAS at
            // the kit's rated (XMP/EXPO) profile — may differ from the applied profile if EXPO is off.
            ["F5 6000J3636F16G"] = new("36-36-36-96"),   // G.Skill DDR5-6000 CL36
        };
}
