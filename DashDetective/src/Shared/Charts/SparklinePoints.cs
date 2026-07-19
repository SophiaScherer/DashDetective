using System;
using System.Globalization;
using System.Text;

namespace DashDetective.Shared.Charts;

/// <summary>
/// Renders a rolling metric history into a <c>Sparkline</c> "x,y x,y …" points string on the control's
/// fixed 0–100 axis. x is the sample index; y is flipped so higher values sit at the top (smaller y =
/// top), matching the Dashboard charts' convention.
///
/// Percentage metrics (CPU, Memory, GPU, Disk) pass <c>valueMax = 100</c> (giving the classic
/// <c>100 − value</c>); unbounded metrics (network throughput) pass a rolling peak so the series still
/// fills the same 0–100 axis without a XAML change.
/// </summary>
public static class SparklinePoints {
    /// <summary>
    /// Builds the points string for <paramref name="history"/> scaled against <paramref name="valueMax"/>.
    /// Each sample is normalised to 0–1 (clamped) then mapped to <c>y = 100 · (1 − ratio)</c>. A
    /// non-positive <paramref name="valueMax"/> pins every point flat at the bottom.
    /// </summary>
    public static string Build(ReadOnlySpan<double> history, double valueMax) {
        var sb = new StringBuilder(history.Length * 8);
        for (var i = 0; i < history.Length; i++) {
            var ratio = valueMax > 0 ? Math.Clamp(history[i] / valueMax, 0, 1) : 0;
            var y = 100 * (1 - ratio);

            if (i > 0)
                sb.Append(' ');
            sb.Append(i.ToString(CultureInfo.InvariantCulture));
            sb.Append(',');
            sb.Append(y.ToString("0.##", CultureInfo.InvariantCulture));
        }

        return sb.ToString();
    }
}
