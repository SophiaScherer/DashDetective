using System;
using System.Collections.Generic;

namespace DashDetective.Tabs.Hardware.Catalog;

/// <summary>Rated motherboard specs WMI can't report — chipset, form factor, M.2 slot count.</summary>
public sealed record BoardSpec(string Chipset, string FormFactor, string M2Slots);

/// <summary>
/// Bundled motherboard spec table, keyed by a distinctive <b>normalized</b> product token (see
/// <see cref="HardwareCatalog.Normalize"/>) — e.g. "MPG B650I EDGE WIFI" matches WMI's
/// "MPG B650I EDGE WIFI (MS-7D73)". Add a board by adding one line. Data is seeded in the Motherboard
/// phase; an empty table leaves the fields as "—" (chipset also has a name-token fallback there).
/// </summary>
internal static class BoardCatalog {
    public static readonly IReadOnlyDictionary<string, BoardSpec> Data =
        new Dictionary<string, BoardSpec>(StringComparer.Ordinal) {
            // Populated in Phase 10.
        };
}
