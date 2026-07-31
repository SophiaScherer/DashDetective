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
        // The page area at a 936px window, where the details pane no longer fits.
        Assert.True(FileExplorerPanes.ShowTree(656));
        Assert.False(FileExplorerPanes.ShowDetails(656));
    }

    [Fact]
    public void BelowTreeThreshold_HidesBoth() {
        // The page area at the 640px window minimum, with the nav rail auto-collapsed to 64px.
        Assert.False(FileExplorerPanes.ShowTree(532));
        Assert.False(FileExplorerPanes.ShowDetails(532));
    }

    [Fact]
    public void DetailsNeverOutlivesTree() {
        // The panes must disappear in a fixed order rather than swapping which one is shown.
        for (var width = 0; width <= 1200; width += 10)
            if (FileExplorerPanes.ShowDetails(width))
                Assert.True(FileExplorerPanes.ShowTree(width));
    }

    [Fact]
    public void Thresholds_CoverTheWidthsThePanesActuallyRenderAt() {
        // Not their MinWidths: the side columns are fixed, so the grid overflows rather than
        // shrinking them, and a threshold based on the minimums lets the page clip.
        Assert.Equal(572, FileExplorerPanes.TreeThreshold, 6);
        Assert.Equal(884, FileExplorerPanes.DetailsThreshold, 6);
    }

    [Fact]
    public void DesignWidth_ClearsTheDetailsThreshold() {
        // The three-pane layout must survive at the size the app opens at.
        Assert.True(DesignWidth >= FileExplorerPanes.DetailsThreshold);
    }
}
