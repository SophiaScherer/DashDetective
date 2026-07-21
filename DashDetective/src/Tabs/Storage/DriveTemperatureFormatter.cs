using System.Globalization;

namespace DashDetective.Tabs.Storage;

/// <summary>Formats a drive temperature (°C) for the drive summary cards: a whole-degree "NN°C", or "—" when
/// there is no reading (non-NVMe drives, or a failed read). Pure, so it is unit-tested directly.</summary>
public static class DriveTemperatureFormatter {
    public static string Format(double? celsius) =>
        celsius is double c ? c.ToString("0", CultureInfo.InvariantCulture) + "°C" : "—";
}
