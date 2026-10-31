using System;

namespace DashDetective.Tabs.Network;

/// <summary>How many output lines fit a console box of a given height. Kept out of the control so it
/// is testable without a render pass, the way ChartAspect is for Sparkline.</summary>
public static class ConsoleCapacity {
    /// <summary>Line height as a multiple of the font size: 15 for the console's 12px text at 100 %.</summary>
    public const double LineRatio = 1.25;

    /// <summary>Vertical padding of the console box (10 above and below).</summary>
    public const double BoxPadding = 20;

    /// <summary>Gap between the output block and the footer line.</summary>
    public const double LineGap = 3;

    /// <summary>The fixed line height for a console font size. Fixed rather than left to the font so the
    /// capacity arithmetic is exact, but derived from the size so a larger Text size spaces its lines
    /// instead of overlapping them.</summary>
    public static double LineHeightFor(double fontSize) =>
        double.IsFinite(fontSize) && fontSize > 0 ? fontSize * LineRatio : 0;

    /// <summary>Height the box spends on everything but output lines: its padding, the footer line and
    /// the gap above it.</summary>
    public static double ReservedFor(double lineHeight) => BoxPadding + lineHeight + LineGap;

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
