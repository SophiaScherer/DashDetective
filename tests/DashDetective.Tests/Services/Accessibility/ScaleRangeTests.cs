using DashDetective.Services.Accessibility;
using Xunit;

namespace DashDetective.Tests.Services.Accessibility;

/// <summary>
/// Covers <see cref="ScaleRange"/>: the one range the interface size and the text size share, the clamp
/// that keeps a hand-edited settings file from collapsing the window, and the rounding that lets a slider
/// and a % field show the stored value exactly.
/// </summary>
public class ScaleRangeTests {
    /// <summary>Both ends have to sit on a step, or neither end of the slider could be selected.</summary>
    [Fact]
    public void TheEndsSitOnAStep() {
        Assert.Equal(0, ScaleRange.MinPercent % ScaleRange.StepPercent);
        Assert.Equal(0, ScaleRange.MaxPercent % ScaleRange.StepPercent);
    }

    /// <summary>100% is the app exactly as it shipped, so it must be reachable and must not be rounded.</summary>
    [Fact]
    public void TheDefaultIsInRangeAndOnAStep() {
        Assert.InRange(ScaleRange.DefaultPercent, ScaleRange.MinPercent, ScaleRange.MaxPercent);
        Assert.Equal(0, ScaleRange.DefaultPercent % ScaleRange.StepPercent);
    }

    [Theory]
    [InlineData(80)]
    [InlineData(100)]
    [InlineData(125)]
    [InlineData(195)]
    [InlineData(200)]
    public void Normalize_AValueOnAStep_ReturnsItUnchanged(int percent) =>
        Assert.Equal(percent, ScaleRange.Normalize(percent));

    /// <summary>The settings file is editable by hand, and 0 or a negative would collapse the window to
    /// nothing rather than degrade — so the clamp is load-bearing, not defensive decoration.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-50)]
    [InlineData(40)]
    [InlineData(79)]
    public void Normalize_BelowTheRange_PinsToTheFloor(int percent) =>
        Assert.Equal(ScaleRange.MinPercent, ScaleRange.Normalize(percent));

    [Theory]
    [InlineData(201)]
    [InlineData(10_000)]
    public void Normalize_AboveTheRange_PinsToTheCeiling(int percent) =>
        Assert.Equal(ScaleRange.MaxPercent, ScaleRange.Normalize(percent));

    /// <summary>A value between steps goes to the closer one. An exact midpoint cannot arise: a 5% step
    /// never halves onto a whole percent, so 82 and 83 are the closest either side gets.</summary>
    [Theory]
    [InlineData(82, 80)]
    [InlineData(83, 85)]
    [InlineData(133, 135)]
    [InlineData(137, 135)]
    [InlineData(162, 160)]
    [InlineData(163, 165)]
    public void Normalize_BetweenSteps_RoundsToTheNearer(int stored, int expected) =>
        Assert.Equal(expected, ScaleRange.Normalize(stored));

    [Theory]
    [InlineData(80, 0.8)]
    [InlineData(100, 1.0)]
    [InlineData(150, 1.5)]
    [InlineData(200, 2.0)]
    public void Factor_ConvertsAPercentage(int percent, double expected) =>
        Assert.Equal(expected, ScaleRange.Factor(percent));

    /// <summary>The factor is taken from the normalized value, so what is in force and what the control
    /// shows cannot disagree.</summary>
    [Theory]
    [InlineData(0, 0.8)]
    [InlineData(133, 1.35)]
    [InlineData(10_000, 2.0)]
    public void Factor_OffAStep_MatchesTheNormalizedValue(int percent, double expected) =>
        Assert.Equal(expected, ScaleRange.Factor(percent));
}
