using DashDetective.Tabs.FileExplorer;
using Xunit;

namespace DashDetective.Tests.Tabs.FileExplorer;

/// <summary>Covers <see cref="FileExplorerPanes"/>: that both side panes fit at the design width, and
/// that they collapse in a fixed order — details first, then the tree — as the page narrows.</summary>
public class FileExplorerPanesTests {
    // The page area at the 1180px design width: window minus the 236px rail and the 44px margins.
    private const double DesignWidth = 900;

    [Fact]
    public void DesignWidth_ShowsBothPanes() {
        Assert.True(FileExplorerPanes.ShowTree(DesignWidth));
        Assert.True(FileExplorerPanes.ShowDetails(DesignWidth));
    }

    [Fact]
    public void BelowDetailsThreshold_HidesDetailsButKeepsTree() {
        Assert.True(FileExplorerPanes.ShowTree(700));
        Assert.False(FileExplorerPanes.ShowDetails(700));
    }

    [Fact]
    public void BelowTreeThreshold_HidesBoth() {
        Assert.False(FileExplorerPanes.ShowTree(480));
        Assert.False(FileExplorerPanes.ShowDetails(480));
    }

    [Fact]
    public void DetailsNeverOutlivesTree() {
        // The panes must disappear in a fixed order rather than swapping which one is shown.
        for (var width = 0; width <= 1200; width += 10)
            if (FileExplorerPanes.ShowDetails(width))
                Assert.True(FileExplorerPanes.ShowTree(width));
    }

    [Fact]
    public void Thresholds_AreTheSumOfTheMinimumsTheyCover() {
        // Tree min + splitter + list min, then the same plus splitter + details min.
        Assert.Equal(512, FileExplorerPanes.TreeThreshold, 6);
        Assert.Equal(744, FileExplorerPanes.DetailsThreshold, 6);
    }
}
