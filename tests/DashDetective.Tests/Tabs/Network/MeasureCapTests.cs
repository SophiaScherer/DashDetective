using DashDetective.Tabs.Network;
using Xunit;

namespace DashDetective.Tests.Tabs.Network;

/// <summary>Covers <see cref="MeasureCap.CappedHeight"/>: the cap bounds what the child is offered when
/// measured, an unbounded measure still gets the cap, and an unset cap changes nothing.</summary>
public class MeasureCapTests {
    [Theory]
    [InlineData(500, 188, 188)]   // tall slot: held to the cap
    [InlineData(120, 188, 120)]   // short slot: the slot wins
    public void CappedHeight_FiniteAvailable_TakesTheSmaller(double available, double cap, double expected) {
        Assert.Equal(expected, MeasureCap.CappedHeight(available, cap));
    }

    [Fact]
    public void CappedHeight_UnboundedAvailable_ReturnsTheCap() {
        Assert.Equal(188, MeasureCap.CappedHeight(double.PositiveInfinity, 188));
    }

    [Fact]
    public void CappedHeight_NoCap_LeavesAvailableAlone() {
        Assert.Equal(300, MeasureCap.CappedHeight(300, double.PositiveInfinity));
        Assert.Equal(300, MeasureCap.CappedHeight(300, double.NaN));
        Assert.True(double.IsPositiveInfinity(MeasureCap.CappedHeight(double.PositiveInfinity, double.NaN)));
    }
}
