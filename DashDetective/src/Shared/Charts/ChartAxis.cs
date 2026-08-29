using Avalonia;
using DashDetective.Shared;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace DashDetective.Shared.Charts;

/// <summary>
/// Where a chart's axis text sits, and what the auto-scaled axis says.
///
/// The charts drew no axis values at all, so a trace's height meant nothing without already knowing the
/// scale. Labelling one costs room, and this is where that room is decided: a left gutter wide enough for
/// the value labels, a footer deep enough for the time-range ends, and neither reserved on a chart that
/// carries no labels — which is every stat-card mini and every per-core cell, so those measure and draw
/// exactly as they did before.
///
/// Pure geometry over <c>Avalonia.Rect</c>, deliberately apart from <c>Sparkline</c>'s rendering: text is
/// measured by the control (only it has the typeface) and composed here, so the layout rules are testable
/// without a render backend.
/// </summary>
public static class ChartAxis {
    /// <summary>Space between the value labels and the plot's left edge.</summary>
    public const double LabelGap = 6;

    /// <summary>Space between the plot's bottom edge and the time-range labels.</summary>
    public const double FooterGap = 3;

    /// <summary>The plot never shrinks below this, however little room the labels leave.</summary>
    public const double MinPlot = 8;

    /// <summary>The left gutter for value labels of the given widths, or 0 when there are none.</summary>
    public static double Gutter(double top, double middle, double bottom) =>
        Gutter(Math.Max(top, Math.Max(middle, bottom)));

    /// <summary>The left gutter for a value axis whose widest label measures this, or 0 when it carries
    /// none.</summary>
    public static double Gutter(double widest) => widest > 0 ? widest + LabelGap : 0;

    /// <summary>The bottom footer for time labels of the given height, or 0 when there are none.</summary>
    public static double Footer(double textHeight) => textHeight > 0 ? textHeight + FooterGap : 0;

    /// <summary>The area the grid and the series draw in, once the gutter and footer are taken out. A
    /// reservation too large for the control is given up rather than inverted — a chart squeezed to
    /// nothing still draws something, and never with a negative size.</summary>
    public static Rect PlotRect(double width, double height, double gutter, double footer) {
        var plotWidth = Math.Max(MinPlot, width - gutter);
        var plotHeight = Math.Max(MinPlot, height - footer);
        var left = Math.Min(gutter, Math.Max(0, width - plotWidth));
        return new Rect(left, 0, plotWidth, plotHeight);
    }

    /// <summary>Places a 1px grid line: snapped to the half-pixel centre of a device pixel so it draws
    /// crisp, and held inside [<paramref name="lower"/>, <paramref name="upper"/>].
    ///
    /// The clamp is what stops the last line of each run drawing half outside the control — with no
    /// clipping on the chart, a bottom line at the exact edge bleeds into the padding of whatever hosts it,
    /// which reads as an unfinished grid on the small charts where the lattice is proportionally largest.
    /// A plot too thin to hold both edges keeps the near one rather than inverting.</summary>
    public static double GridLine(double value, double lower, double upper) {
        var first = Math.Round(lower) + 0.5;
        var last = Math.Round(upper) - 0.5;
        return last <= first ? first : Math.Clamp(Math.Round(value) + 0.5, first, last);
    }

    /// <summary>The three value labels for a throughput chart, whose ceiling moves with the traffic. The
    /// unit rides on the top label only — repeating it on the middle one would widen the gutter for
    /// nothing. Mirrors <see cref="DataRateFormatter"/>'s shared-unit rule, so the axis reads in the same
    /// unit as the readouts beside it.</summary>
    public static (string Top, string Middle, string Bottom) RateLabels(double axisMaxMbps) {
        var labels = RateLabels(axisMaxMbps, 2);
        return (labels[0], labels[1], labels[2]);
    }

    /// <summary>Throughput labels for a chart ruled into <paramref name="divisions"/> bands, top to bottom,
    /// so every label lands on a grid line. Same shared-unit rule as the three-label form.</summary>
    public static IReadOnlyList<string> RateLabels(double axisMaxMbps, int divisions) {
        var unit = DataRateFormatter.UnitFor(axisMaxMbps);
        return Labels(divisions, DataRateFormatter.Label(unit),
            band => DataRateFormatter.FormatValue(
                DataRateFormatter.Convert(axisMaxMbps * band / divisions, unit)));
    }

    /// <summary>Value labels for a percentage axis ruled into <paramref name="divisions"/> bands, top to
    /// bottom: 4 gives "100%" … "0", and 1 gives the ends alone. The sign stays on every reading but the
    /// zero, matching the fixed labels the other tabs' charts carry — it costs no gutter, since the top
    /// label is the widest either way.</summary>
    public static IReadOnlyList<string> PercentLabels(int divisions) =>
        Labels(divisions, "%", band => Math.Round(100.0 * band / divisions)
            .ToString(CultureInfo.InvariantCulture), unitOnTopOnly: false);

    /// <summary>How many of <paramref name="desired"/> labels a plot of this extent can carry: the largest
    /// count that both fits and still lands on the same grid lines, never below the two ends. Takes measured
    /// extents rather than measuring, so the rule is testable without a render backend.</summary>
    public static int FitLabelCount(double plotExtent, double labelExtent, int desired) {
        if (desired <= 2 || labelExtent <= 0)
            return desired;

        for (var count = desired; count > 2; count--) {
            if ((desired - 1) % (count - 1) == 0 && count * labelExtent <= plotExtent)
                return count;
        }
        return 2;
    }

    /// <summary>Builds a top-to-bottom label set over <paramref name="divisions"/> bands. The bottom is a
    /// bare "0" — a zero needs no unit on any scale.</summary>
    private static IReadOnlyList<string> Labels(int divisions, string unit, Func<int, string> format,
                                                bool unitOnTopOnly = true) {
        var bands = Math.Max(1, divisions);
        var labels = new string[bands + 1];
        for (var i = 0; i < bands; i++) {
            var value = format(bands - i);
            labels[i] = i == 0 || !unitOnTopOnly ? $"{value}{Separator(unit)}{unit}" : value;
        }
        labels[bands] = "0";
        return labels;
    }

    /// <summary>A rate unit is a word and takes a space ("80 Mbps"); a percent sign is a suffix and does
    /// not ("100%").</summary>
    private static string Separator(string unit) => unit == "%" ? "" : " ";
}
