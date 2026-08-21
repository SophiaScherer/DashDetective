using DashDetective.Shared.Charts;
using Xunit;

namespace DashDetective.Tests.Shared.Charts;

/// <summary>
/// Covers <see cref="MetricHistory"/>: the rolling window plus how much of it is real. The buffers start
/// full of zeros, and charts that drew all of them showed a launched app a flat line pinned at zero for a
/// whole minute — absent data rendered as measured idle. The fill count is what separates the two.
/// </summary>
public class MetricHistoryTests {
    [Fact]
    public void Push_ShiftsLeftAndAppends() {
        var history = new MetricHistory(3);

        history.Push(1);
        history.Push(2);
        history.Push(3);
        history.Push(4);

        Assert.Equal(new[] { 2.0, 3.0, 4.0 }, history.Values.ToArray());
    }

    [Fact]
    public void Push_LeavesUnreachedSlotsAtZero() {
        var history = new MetricHistory(3);

        history.Push(7);

        Assert.Equal(new[] { 0.0, 0.0, 7.0 }, history.Values.ToArray());
    }

    [Fact]
    public void Filled_CountsSamplesThenSaturatesAtTheWindow() {
        var history = new MetricHistory(3);
        Assert.Equal(0, history.Filled);

        history.Push(1);
        Assert.Equal(1, history.Filled);

        for (var i = 0; i < 10; i++)
            history.Push(i);

        Assert.Equal(3, history.Filled);
    }

    [Fact]
    public void IsWarmingUp_ClearsOnceTheWindowFills() {
        var history = new MetricHistory(2);
        Assert.True(history.IsWarmingUp);

        history.Push(1);
        Assert.True(history.IsWarmingUp);

        history.Push(2);
        Assert.False(history.IsWarmingUp);
    }

    /// <summary>A zero-width window is the no-history channel (the shared feeds, whose subscribers keep
    /// their own buffers). It must swallow pushes rather than throw, and never claim to be warming up.</summary>
    [Fact]
    public void ZeroWindow_SwallowsPushesAndIsNeverWarmingUp() {
        var history = new MetricHistory(0);

        history.Push(5);

        Assert.True(history.Values.IsEmpty);
        Assert.Equal(0, history.Filled);
        Assert.False(history.IsWarmingUp);
    }

    /// <summary>The newest sample keeps the last slot's index whatever the fill, so a partly-filled chart
    /// draws against the right edge instead of stretching across the whole width.</summary>
    [Fact]
    public void Points_PlotsOnlyTheSamplesTaken() {
        var history = new MetricHistory(4);

        history.Push(100);
        history.Push(0);

        Assert.Equal("2,0 3,100", history.Points(100));
    }

    [Fact]
    public void Points_NoSamples_IsEmpty() {
        Assert.Equal("", new MetricHistory(4).Points(100));
    }
}
