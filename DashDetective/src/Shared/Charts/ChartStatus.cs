namespace DashDetective.Shared.Charts;

/// <summary>
/// What a chart says about itself while its window is still filling. One place for the wording, so the
/// four pages that show it cannot drift apart — the <c>ChartWindow</c> shape applied to the cold start.
///
/// Deliberately just the one line: the trace growing in from the right edge already shows the progress, so
/// a countdown beside it would only repeat what the chart is doing.
/// </summary>
public static class ChartStatus {
    /// <summary>Shown over a chart whose rolling window has not filled yet.</summary>
    public const string Collecting = "Collecting data…";

    /// <summary>The status for <paramref name="history"/>: the collecting line, or "" once it is full.</summary>
    public static string For(MetricHistory history) => history.IsWarmingUp ? Collecting : "";
}
