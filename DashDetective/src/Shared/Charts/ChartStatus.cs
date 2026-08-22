namespace DashDetective.Shared.Charts;

/// <summary>
/// What a chart says about itself before it has anything to draw. One place for the wording, so the four
/// pages that show it cannot drift apart — the <c>ChartWindow</c> shape applied to the cold start.
///
/// It clears as soon as a trace appears, NOT when the window finally fills. A line growing in from the
/// right edge already says data is arriving, so holding the label over it for the rest of the minute
/// would only restate what the chart is visibly doing — and it sits on the plot, where it muddles the very
/// thing it is describing. The empty first moment is the only one that needs words.
/// </summary>
public static class ChartStatus {
    /// <summary>Shown over a chart with no trace yet.</summary>
    public const string Collecting = "Collecting data…";

    /// <summary>Samples needed before a chart draws anything: a line wants two points, so one sample is
    /// still a blank chart.</summary>
    private const int DrawableSamples = 2;

    /// <summary>The status for <paramref name="history"/>: the collecting line until it has enough samples
    /// to draw, then "".</summary>
    public static string For(MetricHistory history) =>
        history.Filled < DrawableSamples ? Collecting : "";
}
