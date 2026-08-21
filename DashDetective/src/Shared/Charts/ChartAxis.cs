using Avalonia;
using DashDetective.Shared;
using System;

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
    public static double Gutter(double top, double middle, double bottom) {
        var widest = Math.Max(top, Math.Max(middle, bottom));
        return widest > 0 ? widest + LabelGap : 0;
    }

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

    /// <summary>The three value labels for a throughput chart, whose ceiling moves with the traffic. The
    /// unit rides on the top label only — repeating it on the middle one would widen the gutter for
    /// nothing. Mirrors <see cref="DataRateFormatter"/>'s shared-unit rule, so the axis reads in the same
    /// unit as the readouts beside it.</summary>
    public static (string Top, string Middle, string Bottom) RateLabels(double axisMaxMbps) {
        var unit = DataRateFormatter.UnitFor(axisMaxMbps);
        var top = DataRateFormatter.FormatValue(DataRateFormatter.Convert(axisMaxMbps, unit));
        var middle = DataRateFormatter.FormatValue(DataRateFormatter.Convert(axisMaxMbps / 2, unit));
        return ($"{top} {DataRateFormatter.Label(unit)}", middle, "0");
    }
}
