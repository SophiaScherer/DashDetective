using System.Globalization;

namespace DashDetective.Tabs.Performance;

/// <summary>
/// Formats the GPU "Temp" and "Power" stat tiles. Whole degrees and whole watts — the precision the vendor
/// SDKs actually report, and enough for a tile beside 3D % and VRAM. Always InvariantCulture, matching the
/// app's convention. A missing reading (no vendor reader, an unsupported adapter, a failed call) yields the
/// neutral "—", like every other tile.
/// </summary>
internal static class GpuSensorFormatter {
    /// <summary>Formats a temperature as "64 °C", or "—" when there is no reading.</summary>
    public static string FormatTemperature(double? celsius) {
        // Negated comparison so a NaN reading falls through to the placeholder rather than formatting.
        if (celsius is not { } value || double.IsNaN(value))
            return "—";

        return string.Format(CultureInfo.InvariantCulture, "{0:0} °C", value);
    }

    /// <summary>Formats a power draw as "112 W", or "—" when there is no reading.</summary>
    public static string FormatPower(double? watts) {
        if (watts is not { } value || double.IsNaN(value))
            return "—";

        return string.Format(CultureInfo.InvariantCulture, "{0:0} W", value);
    }
}
