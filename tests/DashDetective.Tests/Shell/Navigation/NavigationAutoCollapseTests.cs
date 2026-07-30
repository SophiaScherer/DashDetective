using DashDetective.Shell.Navigation;
using Xunit;

namespace DashDetective.Tests.Shell.Navigation;

/// <summary>Covers the width-driven rail collapse: that a narrow window folds the bar in without
/// overwriting the user's persisted preference, that widening restores exactly what they chose, and
/// that an explicit toggle still wins while the window is narrow.</summary>
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

    [Fact]
    public void RailWidth_AutoCollapsed_MatchesTheCollapsedRail() {
        var bar = new NavigationViewModel();
        bar.SetShellWidth(Narrow);

        Assert.Equal(64, bar.RailWidth);
        Assert.False(bar.ShowLabels);
    }
}
