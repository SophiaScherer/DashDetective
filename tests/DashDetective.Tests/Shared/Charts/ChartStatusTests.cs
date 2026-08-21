using DashDetective.Shared.Charts;
using Xunit;

namespace DashDetective.Tests.Shared.Charts;

/// <summary>Covers <see cref="ChartStatus"/>: one wording for the cold start, so the four pages that show
/// it cannot drift apart, and nothing at all once the window has filled.</summary>
public class ChartStatusTests {
    [Fact]
    public void For_WarmingUp_SaysItIsCollecting() {
        var history = new MetricHistory(3);
        history.Push(1);

        Assert.Equal("Collecting data…", ChartStatus.For(history));
    }

    [Fact]
    public void For_FullWindow_SaysNothing() {
        var history = new MetricHistory(2);
        history.Push(1);
        history.Push(2);

        Assert.Equal("", ChartStatus.For(history));
    }

    /// <summary>A no-history channel has nothing to collect, so it must not sit on the caption forever.</summary>
    [Fact]
    public void For_ZeroWindow_SaysNothing() {
        Assert.Equal("", ChartStatus.For(new MetricHistory(0)));
    }
}
