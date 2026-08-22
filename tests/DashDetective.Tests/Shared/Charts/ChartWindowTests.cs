using DashDetective.Shared.Charts;
using System;
using Xunit;

namespace DashDetective.Tests.Shared.Charts;

/// <summary>Covers <see cref="ChartWindow"/>: the buffers are a fixed slot count, so the span a chart covers
/// is the Settings refresh interval times that count. Captions read through here so they can't keep claiming
/// "60 seconds", which is only true at the default 1 Hz.</summary>
public class ChartWindowTests {
    [Theory]
    [InlineData(60, 1, 60)]      // the default cadence
    [InlineData(60, 0.5, 30)]    // fastest
    [InlineData(60, 5, 300)]     // slowest
    public void Span_IsTheIntervalTimesTheSlotCount(int samples, double intervalSeconds, double expectedSeconds) {
        var span = ChartWindow.Span(samples, TimeSpan.FromSeconds(intervalSeconds));

        Assert.Equal(expectedSeconds, span.TotalSeconds, 3);
    }

    [Fact]
    public void Span_NoSamples_IsZero() {
        Assert.Equal(TimeSpan.Zero, ChartWindow.Span(0, TimeSpan.FromSeconds(1)));
    }

    [Theory]
    [InlineData(0.5, "30 seconds")]
    [InlineData(1, "60 seconds")]
    [InlineData(2, "2 minutes")]
    [InlineData(5, "5 minutes")]
    public void Describe_ReadsInSecondsThenMinutes(double intervalSeconds, string expected) {
        Assert.Equal(expected, ChartWindow.Describe(60, TimeSpan.FromSeconds(intervalSeconds)));
    }

    /// <summary>The 90-second changeover keeps a second count from reading awkwardly, and a lone minute
    /// stays singular.</summary>
    [Theory]
    [InlineData(89, "89 seconds")]
    [InlineData(90, "1.5 minutes")]
    [InlineData(120, "2 minutes")]
    public void Describe_SwitchesToMinutesAboveNinetySeconds(int samples, string expected) {
        Assert.Equal(expected, ChartWindow.Describe(samples, TimeSpan.FromSeconds(1)));
    }

    /// <summary>The axis end is compact because it sits under the plot's left corner, but it must switch to
    /// minutes at the same point the caption does, or the two would describe one window differently.</summary>
    [Theory]
    [InlineData(60, 0.5, "−30s")]
    [InlineData(60, 1, "−60s")]
    [InlineData(60, 2, "−2m")]
    [InlineData(60, 5, "−5m")]
    [InlineData(89, 1, "−89s")]
    [InlineData(90, 1, "−1.5m")]
    public void StartLabel_IsCompactAndTurnsOverWhereDescribeDoes(int samples, double intervalSeconds, string expected) {
        Assert.Equal(expected, ChartWindow.StartLabel(samples, TimeSpan.FromSeconds(intervalSeconds)));
    }

    [Fact]
    public void EndLabel_IsTheNewestEnd() {
        Assert.Equal("now", ChartWindow.EndLabel);
    }
}
