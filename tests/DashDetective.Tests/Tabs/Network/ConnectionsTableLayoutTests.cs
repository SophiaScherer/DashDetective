using DashDetective.Tabs.Network;
using Xunit;

namespace DashDetective.Tests.Tabs.Network;

/// <summary>Covers <see cref="ConnectionsTableLayout"/>: columns drop in order (Protocol, State, Remote)
/// as the width shrinks, Process never drops, and the minimums grow with the text scale so a larger
/// Text size drops columns at a width where 100 % kept them. Widths are passed in, never measured.</summary>
public class ConnectionsTableLayoutTests {
    // Minimums 96 + 120 + 80 + 60 = 356, plus three 8px gaps.
    private const double AllFit = 356 + 24;

    [Fact]
    public void VisibleCount_WideTable_KeepsEveryColumn() {
        Assert.Equal(4, ConnectionsTableLayout.VisibleCount(AllFit, 1));
        Assert.Equal(4, ConnectionsTableLayout.VisibleCount(2000, 1));
    }

    [Fact]
    public void VisibleCount_NarrowingTable_DropsProtocolThenStateThenRemote() {
        Assert.Equal(3, ConnectionsTableLayout.VisibleCount(AllFit - 1, 1));
        Assert.Equal(2, ConnectionsTableLayout.VisibleCount(96 + 120 + 80 + 16 - 1, 1));
        Assert.Equal(1, ConnectionsTableLayout.VisibleCount(96 + 120 + 8 - 1, 1));
    }

    [Fact]
    public void VisibleCount_TooNarrowForAnything_StillKeepsProcess() {
        Assert.Equal(1, ConnectionsTableLayout.VisibleCount(10, 1));
    }

    [Fact]
    public void VisibleCount_LargerText_DropsColumnsAtAWidthThatFitAllAtOneHundredPercent() {
        Assert.Equal(4, ConnectionsTableLayout.VisibleCount(AllFit + 100, 1));
        Assert.True(ConnectionsTableLayout.VisibleCount(AllFit + 100, 2) < 4);
    }

    [Fact]
    public void VisibleCount_UnusableScale_ActsAsOneHundredPercent() {
        Assert.Equal(ConnectionsTableLayout.VisibleCount(AllFit, 1), ConnectionsTableLayout.VisibleCount(AllFit, 0));
        Assert.Equal(ConnectionsTableLayout.VisibleCount(AllFit, 1), ConnectionsTableLayout.VisibleCount(AllFit, double.NaN));
    }

    [Fact]
    public void VisibleCount_UnconstrainedWidth_KeepsEveryColumn() {
        Assert.Equal(4, ConnectionsTableLayout.VisibleCount(double.PositiveInfinity, 2));
    }

    [Fact]
    public void Definitions_AlwaysFourColumns_DroppedOnesZeroWidth() {
        Assert.Equal("1.2*,1.4*,1*,0.7*", ConnectionsTableLayout.Definitions(2000, 1));
        Assert.Equal("1.2*,1.4*,1*,0*", ConnectionsTableLayout.Definitions(AllFit - 1, 1));
        Assert.Equal("1.2*,1.4*,0*,0*", ConnectionsTableLayout.Definitions(96 + 120 + 80 + 16 - 1, 1));
        Assert.Equal("1*,0*,0*,0*", ConnectionsTableLayout.Definitions(10, 1));
    }

    [Fact]
    public void Definitions_NeverUsesAnAutoColumn() {
        foreach (var width in new[] { 10.0, 250, 380, 2000 })
            Assert.DoesNotContain("Auto", ConnectionsTableLayout.Definitions(width, 1));
    }

    [Theory]
    [InlineData(12, 1)]
    [InlineData(24, 2)]
    [InlineData(18, 1.5)]
    [InlineData(0, 1)]
    [InlineData(-3, 1)]
    [InlineData(double.NaN, 1)]
    public void ScaleOf_HeaderFontSize_IsRatioToTheReferenceSize(double fontSize, double expected) {
        Assert.Equal(expected, ConnectionsTableLayout.ScaleOf(fontSize));
    }
}
