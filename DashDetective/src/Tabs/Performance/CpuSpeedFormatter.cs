using System.Globalization;

namespace DashDetective.Tabs.Performance;

/// <summary>
/// Formats the CPU "Speed" stat tile: the base clock scaled by the live clock ratio, the same figure
/// Task Manager shows. Two decimals of GHz, matching Task Manager's precision (the rail sub-label's
/// base clock keeps one). Always InvariantCulture, matching the app's convention. A missing base clock
/// or a missing reading (the sampler returns 0 when the counter is inert) yields the neutral "—".
/// </summary>
internal static class CpuSpeedFormatter {
    /// <summary>Formats <paramref name="maxClockMhz"/> × <paramref name="performancePercent"/> ÷ 100 as
    /// GHz. The percentage is not capped: it exceeds 100 while the CPU boosts past its base clock, which
    /// is exactly what the readout should show.</summary>
    public static string Format(double maxClockMhz, double performancePercent) {
        // Negated comparisons so a NaN reading falls through to the placeholder rather than formatting.
        if (!(maxClockMhz > 0) || !(performancePercent > 0))
            return "—";

        var ghz = maxClockMhz * performancePercent / 100_000.0;
        return string.Format(CultureInfo.InvariantCulture, "{0:0.00} GHz", ghz);
    }
}
