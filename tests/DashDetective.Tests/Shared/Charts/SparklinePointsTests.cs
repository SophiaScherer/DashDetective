using DashDetective.Shared.Charts;
using System;
using Xunit;

namespace DashDetective.Tests.Shared.Charts;

/// <summary>Covers <see cref="SparklinePoints.Build"/>: the y-flip mapping, 0–1 clamping, the
/// non-positive-max flat case, and InvariantCulture output.</summary>
public class SparklinePointsTests {
    [Fact]
    public void Build_EmptyHistory_ReturnsEmptyString() {
        Assert.Equal("", SparklinePoints.Build(Array.Empty<double>(), 100));
    }

    [Fact]
    public void Build_PercentAxis_FlipsYAndIndexesX() {
        // 100 → top (y 0), 0 → bottom (y 100), 50 → middle (y 50); x is the sample index.
        var result = SparklinePoints.Build(new double[] { 100, 0, 50 }, 100);
        Assert.Equal("0,0 1,100 2,50", result);
    }

    [Fact]
    public void Build_ValueAboveMax_ClampsToTop() {
        Assert.Equal("0,0", SparklinePoints.Build(new double[] { 150 }, 100));
    }

    [Fact]
    public void Build_NegativeValue_ClampsToBottom() {
        Assert.Equal("0,100", SparklinePoints.Build(new double[] { -10 }, 100));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Build_NonPositiveMax_PinsEveryPointFlat(double valueMax) {
        // ratio is forced to 0, so every y is 100 (flat along the bottom).
        Assert.Equal("0,100 1,100", SparklinePoints.Build(new double[] { 50, 80 }, valueMax));
    }

    [Fact]
    public void Build_FormatsYWithInvariantDecimalPoint() {
        // 66.666 / 100 → y = 100·(1−0.66666) = 33.334 → "0.##" → "33.33" (period, not comma).
        Assert.Equal("0,33.33", SparklinePoints.Build(new double[] { 66.666 }, 100));
    }
}
