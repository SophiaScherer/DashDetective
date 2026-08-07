using System;

namespace DashDetective.Tabs.Hardware;

/// <summary>
/// Derives a board's chipset from its product name, for the boards <see cref="Catalog.HardwareCatalog"/>
/// has no entry for. Neither WMI nor sysfs reports the chipset directly, so both platforms fall back to
/// the same token scan — which is why the table lives here rather than privately on either provider.
/// </summary>
internal static class ChipsetNames {
    /// <summary>Chipset (vendor + model) tokens looked up in the board product string, more-specific
    /// variants first (e.g. B650E before B650) so the derived label is the exact chipset.</summary>
    private static readonly (string Token, string Label)[] Chipsets = {
        // AMD (AM5 / AM4)
        ("X670E", "AMD X670E"), ("X670", "AMD X670"), ("B650E", "AMD B650E"), ("B650", "AMD B650"),
        ("A620", "AMD A620"), ("X570", "AMD X570"), ("B550", "AMD B550"), ("A520", "AMD A520"),
        ("X470", "AMD X470"), ("B450", "AMD B450"),
        // Intel (LGA 1700)
        ("Z790", "Intel Z790"), ("Z690", "Intel Z690"), ("B760", "Intel B760"), ("B660", "Intel B660"),
        ("H770", "Intel H770"), ("H670", "Intel H670"), ("Q670", "Intel Q670"), ("H610", "Intel H610"),
    };

    /// <summary>Best-effort chipset from the board product name (e.g. "MPG B650I EDGE" → "AMD B650");
    /// "" when no known token is present.</summary>
    public static string Derive(string product) {
        if (string.IsNullOrWhiteSpace(product))
            return "";

        var upper = product.ToUpperInvariant();
        foreach (var (token, label) in Chipsets) {
            if (upper.Contains(token, StringComparison.Ordinal))
                return label;
        }

        return "";
    }
}
