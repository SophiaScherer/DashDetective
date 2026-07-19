using System;

namespace DashDetective.Shared.Charts;

/// <summary>
/// Shared peak/headroom/floor math for auto-scaling an unbounded chart's vertical axis (network
/// throughput). Centralises the logic the Dashboard, Performance and Network tabs each duplicated.
/// </summary>
public static class ChartScale {
    /// <summary>Largest value across one or two rolling windows (0 when both are empty).</summary>
    public static double Peak(ReadOnlySpan<double> a, ReadOnlySpan<double> b = default) {
        var max = 0.0;
        foreach (var v in a)
            if (v > max) max = v;
        foreach (var v in b)
            if (v > max) max = v;
        return max;
    }

    /// <summary>The peak plus <paramref name="headroom"/> so the top line doesn't touch the edge,
    /// clamped up to <paramref name="floor"/> so an idle graph isn't a full-height spike.</summary>
    public static double FitPeak(double peak, double floor, double headroom = 1.15) {
        var scaled = peak * headroom;
        return scaled > floor ? scaled : floor;
    }

    /// <summary>Convenience: the fitted axis for one or two windows (their peak, with headroom and
    /// floor applied).</summary>
    public static double FitAxis(ReadOnlySpan<double> a, ReadOnlySpan<double> b = default,
                                 double floor = 1.0, double headroom = 1.15) =>
        FitPeak(Peak(a, b), floor, headroom);
}
