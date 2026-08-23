namespace DashDetective.Tabs.Performance;

/// <summary>
/// The plausible bounds a GPU sensor reading has to fall inside to be shown, and the two predicates that
/// apply them. A vendor SDK reports <c>0</c> for a sensor the card does not have and garbage for one it
/// mis-maps, so an unfiltered reading shows a board sitting at absolute zero — or drawing a megawatt.
/// Out of range reads as "no reading" and blanks the tile, which is the honest answer.
///
/// <para>Feature-local on purpose. All three consumers — the NVIDIA, AMD and Linux sensor readers — are
/// in this tab, so the promotion bar for <c>src/Shared</c> is not met. The disk temperature provider has
/// its own narrower ceiling (125 °C) in <c>Services</c>, and that difference is real: a drive at 130 °C
/// is a bad reading where a GPU at 130 °C is merely a hot one.</para>
/// </summary>
internal static class GpuSensorRange {
    private const double MinCelsius = 1;
    private const double MaxCelsius = 150;

    // 0.1 W, not 1 W: an integrated adapter idles below a watt, and the floor is only here to catch a
    // wrong unit scale (a microwatt reading taken as watts lands orders of magnitude under it). The AMD
    // reader used to floor at 1 W and so blanked its own integrated card's idle draw.
    private const double MinWatts = 0.1;
    private const double MaxWatts = 2000;

    /// <summary>The reading in °C, or <c>null</c> when it is outside a plausible GPU range.</summary>
    public static double? Celsius(double? celsius) =>
        celsius is >= MinCelsius and <= MaxCelsius ? celsius : null;

    /// <summary>The reading in watts, or <c>null</c> when it is outside a plausible board range. Callers
    /// holding another unit convert first — this deliberately knows only watts, so a scale mistake shows
    /// up as a rejected reading rather than being absorbed here.</summary>
    public static double? Watts(double? watts) =>
        watts is >= MinWatts and <= MaxWatts ? watts : null;
}
