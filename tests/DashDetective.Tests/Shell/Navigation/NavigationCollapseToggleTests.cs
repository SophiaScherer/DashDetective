using Avalonia.Controls;
using Avalonia.Layout;
using DashDetective.Shell.Navigation;
using System.Collections.Generic;
using Xunit;

namespace DashDetective.Tests.Shell.Navigation;

/// <summary>Covers the collapse toggle — the caret button beside Help in the bar's footer, which replaced
/// the hover-revealed edge puck. Its caret points the way the bar will move, its tooltip (and so its
/// accessible name) says what a press will do, and it stacks with Help the way the footer does.</summary>
public class NavigationCollapseToggleTests {
    private static NavigationViewModel Bar(NavOrientation orientation, bool collapsed) =>
        new() { Orientation = orientation, IsCollapsed = collapsed };

    // Expanded: the caret points at the docked edge (the way the bar will collapse).
    [Theory]
    [InlineData(NavOrientation.Left, ChevronDirection.Left)]
    [InlineData(NavOrientation.Right, ChevronDirection.Right)]
    [InlineData(NavOrientation.Top, ChevronDirection.Up)]
    [InlineData(NavOrientation.Bottom, ChevronDirection.Down)]
    public void ChevronPointing_ExpandedBar_PointsAtTheDockedEdge(
        NavOrientation orientation, ChevronDirection expected) {
        Assert.Equal(expected, Bar(orientation, collapsed: false).ChevronPointing);
    }

    // Collapsed: it flips away from the edge (the way the bar will expand).
    [Theory]
    [InlineData(NavOrientation.Left, ChevronDirection.Right)]
    [InlineData(NavOrientation.Right, ChevronDirection.Left)]
    [InlineData(NavOrientation.Top, ChevronDirection.Down)]
    [InlineData(NavOrientation.Bottom, ChevronDirection.Up)]
    public void ChevronPointing_CollapsedBar_PointsAwayFromTheDockedEdge(
        NavOrientation orientation, ChevronDirection expected) {
        Assert.Equal(expected, Bar(orientation, collapsed: true).ChevronPointing);
    }

    [Fact]
    public void CollapseToolTip_ExpandedBar_OffersToCollapse() {
        Assert.Equal("Collapse navigation", Bar(NavOrientation.Left, collapsed: false).CollapseToolTip);
    }

    [Fact]
    public void CollapseToolTip_CollapsedBar_OffersToExpand() {
        Assert.Equal("Expand navigation", Bar(NavOrientation.Left, collapsed: true).CollapseToolTip);
    }

    // A narrow window folds the rail without the user asking; the toggle must still describe what it shows.
    [Fact]
    public void CollapseToolTip_AutoCollapsedBar_OffersToExpand() {
        var bar = Bar(NavOrientation.Left, collapsed: false);
        bar.SetShellWidth(NavigationViewModel.AutoCollapseWidth - 1);

        Assert.Equal("Expand navigation", bar.CollapseToolTip);
        Assert.Equal(ChevronDirection.Right, bar.ChevronPointing);
    }

    // 64px is too narrow for Help and the toggle side by side, so a collapsed vertical rail stacks them.
    [Theory]
    [InlineData(NavOrientation.Left, true, Orientation.Vertical, Dock.Bottom)]
    [InlineData(NavOrientation.Right, true, Orientation.Vertical, Dock.Bottom)]
    [InlineData(NavOrientation.Left, false, Orientation.Horizontal, Dock.Right)]
    [InlineData(NavOrientation.Top, true, Orientation.Horizontal, Dock.Right)]
    [InlineData(NavOrientation.Bottom, false, Orientation.Horizontal, Dock.Right)]
    public void FooterControls_StackInAColumnOnlyOnACollapsedVerticalRail(
        NavOrientation orientation, bool collapsed, Orientation expected, Dock dock) {
        var bar = Bar(orientation, collapsed);

        Assert.Equal(expected, bar.ControlsOrientation);
        Assert.Equal(dock, bar.ControlsDock);
    }

    [Fact]
    public void ToggleCollapse_FlipsTheBarAndReversesTheCaret() {
        var bar = Bar(NavOrientation.Left, collapsed: false);

        bar.ToggleCollapseCommand.Execute(null);

        Assert.True(bar.IsCollapsed);
        Assert.Equal(ChevronDirection.Right, bar.ChevronPointing);
        Assert.Equal("Expand navigation", bar.CollapseToolTip);
    }

    [Fact]
    public void Collapsing_RaisesChangeNotificationForTheToggle() {
        var bar = Bar(NavOrientation.Left, collapsed: false);
        var changed = new List<string?>();
        bar.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        bar.IsCollapsed = true;

        Assert.Contains(nameof(NavigationViewModel.ChevronPointing), changed);
        Assert.Contains(nameof(NavigationViewModel.ChevronIcon), changed);
        Assert.Contains(nameof(NavigationViewModel.CollapseToolTip), changed);
        Assert.Contains(nameof(NavigationViewModel.ControlsOrientation), changed);
    }

    [Fact]
    public void AutoCollapsing_RaisesChangeNotificationForTheToggle() {
        var bar = Bar(NavOrientation.Left, collapsed: false);
        var changed = new List<string?>();
        bar.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        bar.IsAutoCollapsed = true;

        Assert.Contains(nameof(NavigationViewModel.CollapseToolTip), changed);
        Assert.Contains(nameof(NavigationViewModel.ControlsOrientation), changed);
    }

    [Fact]
    public void Redocking_RaisesChangeNotificationForTheToggle() {
        var bar = Bar(NavOrientation.Left, collapsed: true);
        var changed = new List<string?>();
        bar.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        bar.Orientation = NavOrientation.Top;

        Assert.Contains(nameof(NavigationViewModel.ChevronPointing), changed);
        Assert.Contains(nameof(NavigationViewModel.ChevronIcon), changed);
        Assert.Contains(nameof(NavigationViewModel.ControlsOrientation), changed);
    }
}
