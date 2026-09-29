using DashDetective.Shell.Navigation;
using Xunit;

namespace DashDetective.Tests.Shell.Navigation;

/// <summary>Covers the width-driven rail collapse: that a narrow window folds the bar in without
/// overwriting the user's persisted preference, that widening restores exactly what they chose, that
/// an explicit toggle still wins while the window is narrow, and that the threshold follows the
/// interface size — the width is the window's, while the rail it is protecting is scaled. The breakpoint
/// itself is <see cref="NavigationViewModel.AutoCollapseThreshold"/>: the rail plus a minimum page for a
/// vertical rail, the measured labeled width for a horizontal bar.</summary>
public class NavigationAutoCollapseTests {
    private const double Narrow = NavigationViewModel.AutoCollapseWidth - 1;
    private const double Wide = NavigationViewModel.AutoCollapseWidth + 1;

    [Fact]
    public void SetShellWidth_Narrow_CollapsesTheRail() {
        var bar = new NavigationViewModel();
        bar.SetShellWidth(Narrow);

        Assert.True(bar.IsRailCollapsed);
        Assert.True(bar.IsAutoCollapsed);
    }

    [Fact]
    public void SetShellWidth_Narrow_LeavesThePreferenceAlone() {
        // The persisted flag must not move, or the next launch would open collapsed by accident.
        var bar = new NavigationViewModel();
        bar.SetShellWidth(Narrow);

        Assert.False(bar.IsCollapsed);
    }

    [Fact]
    public void SetShellWidth_WidenedAgain_RestoresTheExpandedPreference() {
        var bar = new NavigationViewModel();
        bar.SetShellWidth(Narrow);
        bar.SetShellWidth(Wide);

        Assert.False(bar.IsRailCollapsed);
    }

    [Fact]
    public void SetShellWidth_WidenedAgain_KeepsACollapsedPreferenceCollapsed() {
        var bar = new NavigationViewModel { IsCollapsed = true };
        bar.SetShellWidth(Narrow);
        bar.SetShellWidth(Wide);

        Assert.True(bar.IsRailCollapsed);
        Assert.True(bar.IsCollapsed);
    }

    [Fact]
    public void SetShellWidth_AtTheThreshold_StaysExpanded() {
        var bar = new NavigationViewModel();
        bar.SetShellWidth(NavigationViewModel.AutoCollapseWidth);

        Assert.False(bar.IsRailCollapsed);
    }

    [Fact]
    public void SetShellWidth_NonFinite_IsIgnored() {
        // Layout can report a zero or NaN width before the window is measured.
        var bar = new NavigationViewModel();
        bar.SetShellWidth(0);
        bar.SetShellWidth(double.NaN);

        Assert.False(bar.IsRailCollapsed);
    }

    [Fact]
    public void ToggleCollapse_WhileAutoCollapsed_Expands() {
        // Otherwise the control would appear to do nothing on a narrow window.
        var bar = new NavigationViewModel();
        bar.SetShellWidth(Narrow);
        bar.ToggleCollapseCommand.Execute(null);

        Assert.False(bar.IsRailCollapsed);
        Assert.False(bar.IsAutoCollapsed);
    }

    [Fact]
    public void SetShellWidth_StillNarrowAfterAToggle_DoesNotReCollapse() {
        // Auto-collapse acts on a threshold crossing, so an explicit choice sticks until the window
        // actually crosses back.
        var bar = new NavigationViewModel();
        bar.SetShellWidth(Narrow);
        bar.ToggleCollapseCommand.Execute(null);
        bar.SetShellWidth(Narrow - 50);

        Assert.False(bar.IsRailCollapsed);
    }

    [Fact]
    public void SetShellWidth_CrossingBackAndForth_ReAppliesTheCollapse() {
        var bar = new NavigationViewModel();
        bar.SetShellWidth(Narrow);
        bar.ToggleCollapseCommand.Execute(null);
        bar.SetShellWidth(Wide);
        bar.SetShellWidth(Narrow);

        Assert.True(bar.IsRailCollapsed);
    }

    /// <summary>A window wide enough at 100% is not wide enough at 200%.</summary>
    [Fact]
    public void SetUiScale_Enlarged_CollapsesAWindowThatWasWideEnough() {
        var bar = new NavigationViewModel();
        bar.SetShellWidth(Wide);

        bar.SetUiScale(2);

        Assert.True(bar.IsRailCollapsed);
    }

    /// <summary>And the other way: below 100% the rail needs fewer window pixels, so a window the
    /// threshold would have folded stays expanded.</summary>
    [Fact]
    public void SetUiScale_Reduced_LeavesANarrowWindowExpanded() {
        var bar = new NavigationViewModel();
        bar.SetUiScale(0.8);
        bar.SetShellWidth(Narrow);

        Assert.False(bar.IsRailCollapsed);
    }

    /// <summary>A scale reported before any width must not fold a bar whose window is unknown.</summary>
    [Fact]
    public void SetUiScale_BeforeAnyWidth_LeavesTheRailAlone() {
        var bar = new NavigationViewModel();
        bar.SetUiScale(2);

        Assert.False(bar.IsRailCollapsed);
    }

    /// <summary>The raised breakpoint: an expanded rail folds before the page beside it drops under
    /// <see cref="NavigationViewModel.MinPageWidth"/>, which lands on Fluent's 1008.</summary>
    [Fact]
    public void AutoCollapseWidth_IsTheExpandedRailPlusTheNarrowestPage() {
        Assert.Equal(1008, NavigationViewModel.AutoCollapseWidth);
        Assert.Equal(NavigationViewModel.AutoCollapseWidth, new NavigationViewModel().AutoCollapseThreshold);
    }

    /// <summary>The old breakpoint ignored text size, so at 200% a 472px rail kept its labels until the
    /// page beside it was down to about 350px.</summary>
    [Fact]
    public void AutoCollapseThreshold_LargeText_CountsTheWiderRail() {
        var bar = new NavigationViewModel();
        bar.SetTextScale(2);

        Assert.Equal(472 + NavigationViewModel.MinPageWidth, bar.AutoCollapseThreshold);
    }

    // The rail's icons do not shrink with smaller text, so neither does the room it takes.
    [Fact]
    public void AutoCollapseThreshold_SmallText_KeepsTheAuthoredRail() {
        var bar = new NavigationViewModel();
        bar.SetTextScale(0.8);

        Assert.Equal(NavigationViewModel.AutoCollapseWidth, bar.AutoCollapseThreshold);
    }

    [Fact]
    public void SetTextScale_Enlarged_CollapsesAWindowThatWasWideEnough() {
        var bar = new NavigationViewModel();
        bar.SetShellWidth(1100);
        Assert.False(bar.IsRailCollapsed);

        bar.SetTextScale(2);

        Assert.True(bar.IsRailCollapsed);
    }

    // Until an expanded horizontal bar has been laid out there is nothing measured, so it folds where a
    // vertical rail would rather than never.
    [Fact]
    public void AutoCollapseThreshold_HorizontalBarNotYetMeasured_UsesTheRailBreakpoint() {
        var bar = new NavigationViewModel { Orientation = NavOrientation.Top };
        Assert.Equal(NavigationViewModel.AutoCollapseWidth, bar.AutoCollapseThreshold);
    }

    /// <summary>A horizontal bar folds when its labeled items stop fitting, so labels never scroll out of
    /// sight before the switch: the fold point is the window width plus the strip's overrun.</summary>
    [Fact]
    public void ReportLabelOverflow_ItemsOverflow_FoldsTheBar() {
        var bar = new NavigationViewModel { Orientation = NavOrientation.Top };
        bar.SetShellWidth(1100);
        Assert.False(bar.IsRailCollapsed);

        bar.ReportLabelOverflow(viewport: 900, content: 1170);

        Assert.Equal(1370, bar.AutoCollapseThreshold);
        Assert.True(bar.IsRailCollapsed);
    }

    [Fact]
    public void ReportLabelOverflow_ItemsFitWithRoomToSpare_KeepsTheLabels() {
        var bar = new NavigationViewModel { Orientation = NavOrientation.Top };
        bar.SetShellWidth(1100);

        bar.ReportLabelOverflow(viewport: 900, content: 700);

        Assert.Equal(900, bar.AutoCollapseThreshold);
        Assert.False(bar.IsRailCollapsed);
    }

    /// <summary>An exact fit keeps the labels, and the first pixel of overrun folds them: that pixel is
    /// where the strip's scroll bar would appear.</summary>
    [Fact]
    public void ReportLabelOverflow_ExactFit_KeepsTheLabels_AndOnePixelOverFolds() {
        var bar = new NavigationViewModel { Orientation = NavOrientation.Top };
        bar.SetShellWidth(1100);
        bar.ReportLabelOverflow(viewport: 900, content: 900);
        Assert.False(bar.IsRailCollapsed);

        bar.SetShellWidth(1099);

        Assert.True(bar.IsRailCollapsed);
    }

    /// <summary>The bug this pins: the need was taken from the bar's own width, which the window's frame
    /// keeps narrower than the window width it is compared with, so the labels overflowed into a scroll
    /// bar for several pixels before the fold. Measured as an overrun, the frame cancels out.</summary>
    [Fact]
    public void ReportLabelOverflow_IsMeasuredAgainstTheWindowNotTheBar() {
        var bar = new NavigationViewModel { Orientation = NavOrientation.Top };
        bar.SetShellWidth(1560);

        bar.ReportLabelOverflow(viewport: 1180, content: 1184);

        Assert.Equal(1564, bar.AutoCollapseThreshold);
        Assert.True(bar.IsRailCollapsed);
    }

    // With the labels hidden the strip holds only icons, which would claim the labels fit.
    [Fact]
    public void ReportLabelOverflow_WhileCollapsed_IsIgnored() {
        var bar = new NavigationViewModel { Orientation = NavOrientation.Top, IsCollapsed = true };
        bar.SetShellWidth(1100);

        bar.ReportLabelOverflow(viewport: 900, content: 400);

        Assert.Equal(NavigationViewModel.AutoCollapseWidth, bar.AutoCollapseThreshold);
    }

    [Fact]
    public void ReportLabelOverflow_VerticalRail_IsIgnored() {
        var bar = new NavigationViewModel();
        bar.SetShellWidth(1100);

        bar.ReportLabelOverflow(viewport: 700, content: 900);

        Assert.Equal(NavigationViewModel.AutoCollapseWidth, bar.AutoCollapseThreshold);
    }

    // Before any window width is known there is nothing to add the overrun to.
    [Fact]
    public void ReportLabelOverflow_BeforeAnyWidth_IsIgnored() {
        var bar = new NavigationViewModel { Orientation = NavOrientation.Top };

        bar.ReportLabelOverflow(viewport: 900, content: 1170);

        Assert.Equal(NavigationViewModel.AutoCollapseWidth, bar.AutoCollapseThreshold);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void ReportLabelOverflow_UnusableMeasure_IsIgnored(double content) {
        var bar = new NavigationViewModel { Orientation = NavOrientation.Top };
        bar.SetShellWidth(1100);

        bar.ReportLabelOverflow(viewport: 900, content: content);

        Assert.Equal(NavigationViewModel.AutoCollapseWidth, bar.AutoCollapseThreshold);
    }

    // The strip is drawn inside the scale host, so each logical pixel of overrun costs that many window pixels.
    [Fact]
    public void ReportLabelOverflow_ScalesTheOverrunByTheInterfaceSize() {
        var bar = new NavigationViewModel { Orientation = NavOrientation.Top };
        bar.SetUiScale(2);
        bar.SetShellWidth(2200);

        bar.ReportLabelOverflow(viewport: 900, content: 1170);

        Assert.Equal(2740, bar.AutoCollapseThreshold);
    }

    // A measurement in window pixels is only good for the interface size it was taken at.
    [Fact]
    public void SetUiScale_ForgetsTheLabeledWidth() {
        var bar = new NavigationViewModel { Orientation = NavOrientation.Top };
        bar.SetShellWidth(1100);
        bar.ReportLabelOverflow(viewport: 900, content: 1170);

        bar.SetUiScale(1.5);

        Assert.Equal(NavigationViewModel.AutoCollapseWidth * 1.5, bar.AutoCollapseThreshold);
    }

    // The labels change width with the text, so a measure taken at the old size no longer says where
    // they stop fitting.
    [Fact]
    public void SetTextScale_ForgetsTheLabeledWidth() {
        var bar = new NavigationViewModel { Orientation = NavOrientation.Top };
        bar.SetShellWidth(1100);
        bar.ReportLabelOverflow(viewport: 900, content: 1170);

        bar.SetTextScale(1.5);

        Assert.Equal(354 + NavigationViewModel.MinPageWidth, bar.AutoCollapseThreshold);
    }

    // A horizontal bar can need more width than a vertical rail's breakpoint, so re-docking re-tests.
    [Fact]
    public void Redocking_ReTestsTheBreakpointForTheNewEdge() {
        var bar = new NavigationViewModel { Orientation = NavOrientation.Top };
        bar.SetShellWidth(1100);
        bar.ReportLabelOverflow(viewport: 900, content: 1170);
        Assert.True(bar.IsRailCollapsed);

        bar.Orientation = NavOrientation.Left;

        Assert.False(bar.IsRailCollapsed);
    }

    [Fact]
    public void RailWidth_AutoCollapsed_MatchesTheCollapsedRail() {
        var bar = new NavigationViewModel();
        bar.SetShellWidth(Narrow);

        Assert.Equal(64, bar.RailWidth);
        Assert.False(bar.ShowLabels);
    }
}
