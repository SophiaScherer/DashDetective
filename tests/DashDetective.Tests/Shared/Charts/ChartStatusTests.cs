using DashDetective.Shared.Charts;
using Xunit;

namespace DashDetective.Tests.Shared.Charts;

/// <summary>Covers <see cref="ChartStatus"/>: one wording for the cold start, so the four pages that show
/// it cannot drift apart, and nothing at all once the chart has a trace of its own.</summary>
public class ChartStatusTests {
    [Fact]
    public void For_NoSamples_SaysItIsCollecting() {
        Assert.Equal("Collecting data…", ChartStatus.For(new MetricHistory(60)));
    }

    /// <summary>One sample is a point, not a line, so the chart is still blank and still needs words.</summary>
    [Fact]
    public void For_OneSample_StillSaysItIsCollecting() {
        var history = new MetricHistory(60);
        history.Push(1);

        Assert.Equal("Collecting data…", ChartStatus.For(history));
    }

    /// <summary>The load-bearing one: the label goes as soon as there is a line, long before the window
    /// fills. A trace growing in from the right already says data is arriving, and the label sits on the
    /// plot it would be describing.</summary>
    [Fact]
    public void For_EnoughToDraw_SaysNothingEvenWithTheWindowUnfilled() {
        var history = new MetricHistory(60);
        history.Push(1);
        history.Push(2);

        Assert.Equal("", ChartStatus.For(history));
    }

    /// <summary>A no-history channel has nothing to collect, but it also never draws — the caption belongs
    /// to charts, and a page without one simply never shows it.</summary>
    [Fact]
    public void For_ZeroWindow_SaysItIsCollecting() {
        Assert.Equal("Collecting data…", ChartStatus.For(new MetricHistory(0)));
    }
}
