namespace DashDetective.Shared.Charts;

/// <summary>
/// Turns a chart's measured width into a height at a fixed width:height ratio, so a chart keeps its
/// shape as its slot resizes instead of being squashed. Lives here (rather than in the control) so
/// the arithmetic is testable without a layout pass.
/// </summary>
public static class ChartAspect {
    /// <summary>Height for a chart <paramref name="width"/> px wide at <paramref name="ratio"/>
    /// (width ÷ height), clamped to [<paramref name="minHeight"/>, <paramref name="maxHeight"/>].
    /// A non-finite width or a ratio that isn't a positive number falls back to a finite
    /// <paramref name="maxHeight"/>, else <paramref name="minHeight"/>, so a chart in an
    /// unconstrained slot still shows something rather than collapsing.</summary>
    public static double HeightForWidth(double width, double ratio,
                                        double minHeight = 0,
                                        double maxHeight = double.PositiveInfinity) {
        if (double.IsNaN(ratio) || ratio <= 0 || !double.IsFinite(width))
            return Clamp(double.IsFinite(maxHeight) ? maxHeight : minHeight, minHeight, maxHeight);

        return Clamp(width > 0 ? width / ratio : 0, minHeight, maxHeight);
    }

    /// <summary>Clamps the way Avalonia's layout system does (see <c>Avalonia.Layout.MinMax</c>):
    /// the minimum wins when it exceeds the maximum. Keeps this in step with the clamp
    /// <c>MeasureCore</c> applies to the measured height afterwards.</summary>
    private static double Clamp(double value, double min, double max) {
        if (value > max)
            value = max;
        if (value < min)
            value = min;
        return value;
    }
}
