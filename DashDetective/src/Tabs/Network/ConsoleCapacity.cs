using System;

namespace DashDetective.Tabs.Network;

/// <summary>How many output lines fit a console box of a given height. Kept out of the control so it
/// is testable without a render pass, the way ChartAspect is for Sparkline.</summary>
public static class ConsoleCapacity {
    /// <summary>Lines that fit <paramref name="boxHeight"/> once <paramref name="reserved"/> (padding,
    /// the footer line and its gap) is taken out, clamped to <paramref name="min"/>..<paramref name="max"/>.
    /// A box too small for even one line still reports the minimum, so the readout never empties.</summary>
    public static int LinesForHeight(double boxHeight, double lineHeight, double reserved,
                                     int min, int max) {
        if (max < min)
            max = min;
        if (lineHeight <= 0 || !double.IsFinite(boxHeight))
            return min;

        var usable = boxHeight - reserved;
        if (usable <= 0)
            return min;

        return Math.Clamp((int)Math.Floor(usable / lineHeight), min, max);
    }
}
