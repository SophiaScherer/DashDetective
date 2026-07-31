using DashDetective.Tabs.FileExplorer;
using Xunit;

namespace DashDetective.Tests.Tabs.FileExplorer;

/// <summary>Covers <see cref="FileExplorerTableLayout"/>: that the narrow design-size pane still
/// shows every column, the drop order below it, and the fixed four-slot definitions string.</summary>
public class FileExplorerTableLayoutTests {
    // The list sits between two splitters, so it is only ~291px wide at the 1180px design size.
    private const double DesignWidth = 291;

    [Fact]
    public void Definitions_DesignWidth_KeepsAllFour() {
        // The pane is already narrow here, so no column may drop at the size the app opens at.
        Assert.Equal("2.2*,1*,1.2*,0.9*", FileExplorerTableLayout.Definitions(DesignWidth));
        Assert.Equal(4, FileExplorerTableLayout.VisibleCount(DesignWidth));
    }

    [Fact]
    public void Definitions_DropsModifiedFirst() {
        // All four need 258 + 24 = 282; three need 198 + 16 = 214.
        Assert.Equal("2.2*,1*,0*,0.9*", FileExplorerTableLayout.Definitions(250));
    }

    [Fact]
    public void Definitions_ThenDropsType() {
        Assert.Equal("2.2*,0*,0*,0.9*", FileExplorerTableLayout.Definitions(200));
    }

    [Fact]
    public void Definitions_AlwaysHaveFourSlots() {
        foreach (var width in new double[] { 100, 200, 250, 291, 600 })
            Assert.Equal(4, FileExplorerTableLayout.Definitions(width).Split(',').Length);
    }

    [Fact]
    public void VisibleCount_VeryNarrow_NeverDropsBelowTwo() {
        Assert.Equal(2, FileExplorerTableLayout.VisibleCount(40));
    }

    [Fact]
    public void Flags_FollowTheDropOrder() {
        Assert.True(FileExplorerTableLayout.ShowModified(DesignWidth));
        Assert.True(FileExplorerTableLayout.ShowType(DesignWidth));

        Assert.False(FileExplorerTableLayout.ShowModified(250));
        Assert.True(FileExplorerTableLayout.ShowType(250));

        Assert.False(FileExplorerTableLayout.ShowType(200));
    }
}
