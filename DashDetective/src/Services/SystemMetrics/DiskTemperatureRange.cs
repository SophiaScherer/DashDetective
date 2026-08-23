namespace DashDetective.Services.SystemMetrics;

/// <summary>
/// The plausible bounds a drive temperature has to fall inside to be shown. A sensor reports <c>0</c>
/// for a value it does not have — and NVMe reports it in Kelvin, so an absent reading arrives as a
/// wildly negative °C — and either would otherwise draw a drive sitting at freezing. Out of range reads
/// as "no reading" and blanks the Temp cell.
///
/// <para>Both platform arms apply the same bounds and had said so in prose ("matching the Windows arm")
/// while keeping their own copies of the constants. The ceiling is deliberately lower than the GPU's
/// (see <c>Tabs/Performance/GpuSensorRange</c>): a drive reading 130 °C is a bad reading, where a GPU at
/// 130 °C is merely a hot one.</para>
/// </summary>
internal static class DiskTemperatureRange {
    private const double MinCelsius = 1;
    private const double MaxCelsius = 125;

    /// <summary>The reading in °C, or <c>null</c> when it is outside a plausible drive range.</summary>
    public static double? Celsius(double? celsius) =>
        celsius is >= MinCelsius and <= MaxCelsius ? celsius : null;
}
