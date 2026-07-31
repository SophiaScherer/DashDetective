using DashDetective.Tabs.Processes;
using Xunit;

namespace DashDetective.Tests.Tabs.Processes;

/// <summary>Covers <see cref="ProcessTableLayout"/>: the drop order, the four columns that never
/// drop, and that the definitions string always keeps seven slots so cell indices stay fixed.</summary>
public class ProcessTableLayoutTests {
    // Widths the table's panel actually gets: ~838 at the 1180px design size, ~638 at 980.
    private const double DesignWidth = 838;
    private const double NarrowWidth = 638;

    [Fact]
    public void Definitions_DesignWidth_KeepsAllSeven() {
        Assert.Equal("2.4*,0.7*,1*,0.85*,0.85*,0.85*,0.85*", ProcessTableLayout.Definitions(DesignWidth));
        Assert.Equal(7, ProcessTableLayout.VisibleCount(DesignWidth));
    }

    [Fact]
    public void Definitions_At980_StillKeepsAllSeven() {
        // The columns are tight but readable here, so nothing should drop yet.
        Assert.Equal(7, ProcessTableLayout.VisibleCount(NarrowWidth));
    }

    [Fact]
    public void Definitions_DropsGpuFirst() {
        // All seven need 552 + 24 = 576; six need 496 + 20 = 516.
        Assert.Equal("2.4*,0.7*,1*,0.85*,0.85*,0.85*,0*", ProcessTableLayout.Definitions(560));
    }

    [Fact]
    public void Definitions_ThenDropsDisk() {
        // Five need 430 + 16 = 446.
        Assert.Equal("2.4*,0.7*,1*,0.85*,0.85*,0*,0*", ProcessTableLayout.Definitions(500));
    }

    [Fact]
    public void Definitions_ThenDropsStatus() {
        Assert.Equal("2.4*,0.7*,0*,0.85*,0.85*,0*,0*", ProcessTableLayout.Definitions(400));
    }

    [Fact]
    public void Definitions_AlwaysHaveSevenSlots() {
        // Cell Grid.Column indices are static, so the slot count must never change.
        foreach (var width in new double[] { 200, 400, 500, 560, 638, 838, 5000 })
            Assert.Equal(7, ProcessTableLayout.Definitions(width).Split(',').Length);
    }

    [Fact]
    public void VisibleCount_VeryNarrow_NeverDropsBelowFour() {
        Assert.Equal(4, ProcessTableLayout.VisibleCount(50));
    }

    [Fact]
    public void Flags_FollowTheDropOrder() {
        Assert.True(ProcessTableLayout.ShowGpu(DesignWidth));
        Assert.True(ProcessTableLayout.ShowDisk(DesignWidth));
        Assert.True(ProcessTableLayout.ShowStatus(DesignWidth));

        Assert.False(ProcessTableLayout.ShowGpu(560));
        Assert.True(ProcessTableLayout.ShowDisk(560));

        Assert.False(ProcessTableLayout.ShowDisk(500));
        Assert.True(ProcessTableLayout.ShowStatus(500));

        Assert.False(ProcessTableLayout.ShowStatus(400));
    }
}
