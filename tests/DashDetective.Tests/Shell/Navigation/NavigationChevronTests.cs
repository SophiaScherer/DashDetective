using Avalonia;
using Avalonia.Layout;
using DashDetective.Shell.Navigation;
using System.Collections.Generic;
using Xunit;

namespace DashDetective.Tests.Shell.Navigation;

/// <summary>Covers the collapse/expand puck — the semi-circle that straddles the bar's content-facing
/// edge. Its chevron must point the way the bar will move, and its size, alignment, overhang and
/// rounding must all follow the docked edge (all computed on the view model, no converters).</summary>
public class NavigationChevronTests {
    // Half the puck's 18px thickness: how far it hangs past the bar's edge into the content area.
    private const double Overhang = -9;

    private static NavigationViewModel Bar(NavOrientation orientation, bool collapsed) =>
        new() { Orientation = orientation, IsCollapsed = collapsed };

    // Expanded: the chevron points at the docked edge (the way the bar will collapse).
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

    // The puck is long along the edge it sits on and thin across it, whichever edge that is.
    [Theory]
    [InlineData(NavOrientation.Left)]
    [InlineData(NavOrientation.Right)]
    public void ChevronSize_VerticalRail_IsThinAndTall(NavOrientation orientation) {
        var bar = Bar(orientation, collapsed: false);
        Assert.Equal(18, bar.ChevronWidth);
        Assert.Equal(40, bar.ChevronHeight);
    }

    [Theory]
    [InlineData(NavOrientation.Top)]
    [InlineData(NavOrientation.Bottom)]
    public void ChevronSize_HorizontalBar_IsWideAndShort(NavOrientation orientation) {
        var bar = Bar(orientation, collapsed: false);
        Assert.Equal(40, bar.ChevronWidth);
        Assert.Equal(18, bar.ChevronHeight);
    }

    [Theory]
    [InlineData(NavOrientation.Left, HorizontalAlignment.Right, VerticalAlignment.Center)]
    [InlineData(NavOrientation.Right, HorizontalAlignment.Left, VerticalAlignment.Center)]
    [InlineData(NavOrientation.Top, HorizontalAlignment.Center, VerticalAlignment.Bottom)]
    [InlineData(NavOrientation.Bottom, HorizontalAlignment.Center, VerticalAlignment.Top)]
    public void ChevronAlignment_CentresOnTheContentFacingEdge(
        NavOrientation orientation, HorizontalAlignment horizontal, VerticalAlignment vertical) {
        var bar = Bar(orientation, collapsed: false);
        Assert.Equal(horizontal, bar.ChevronHAlign);
        Assert.Equal(vertical, bar.ChevronVAlign);
    }

    // A negative margin on the content-facing side only — the other three stay zero, so the puck is
    // pulled out by exactly half its thickness and straddles the edge.
    [Theory]
    [InlineData(NavOrientation.Left, 0, 0, Overhang, 0)]
    [InlineData(NavOrientation.Right, Overhang, 0, 0, 0)]
    [InlineData(NavOrientation.Top, 0, 0, 0, Overhang)]
    [InlineData(NavOrientation.Bottom, 0, Overhang, 0, 0)]
    public void ChevronMargin_StraddlesTheEdge(
        NavOrientation orientation, double left, double top, double right, double bottom) {
        Assert.Equal(new Thickness(left, top, right, bottom), Bar(orientation, collapsed: false).ChevronMargin);
    }

    // Only the overhanging half is rounded, so the puck reads as a semi-circle growing out of the bar.
    [Theory]
    [InlineData(NavOrientation.Left, 0d, 40d, 40d, 0d)]
    [InlineData(NavOrientation.Right, 40d, 0d, 0d, 40d)]
    [InlineData(NavOrientation.Top, 0d, 0d, 40d, 40d)]
    [InlineData(NavOrientation.Bottom, 40d, 40d, 0d, 0d)]
    public void ChevronCornerRadius_RoundsOnlyTheOverhangingHalf(
        NavOrientation orientation, double topLeft, double topRight, double bottomRight, double bottomLeft) {
        Assert.Equal(new CornerRadius(topLeft, topRight, bottomRight, bottomLeft),
            Bar(orientation, collapsed: false).ChevronCornerRadius);
    }

    [Fact]
    public void ToggleCollapse_FlipsTheBarAndReversesTheChevron() {
        var bar = Bar(NavOrientation.Left, collapsed: false);

        bar.ToggleCollapseCommand.Execute(null);

        Assert.True(bar.IsCollapsed);
        Assert.Equal(ChevronDirection.Right, bar.ChevronPointing);
    }

    [Fact]
    public void Collapsing_RaisesChangeNotificationForTheChevron() {
        var bar = Bar(NavOrientation.Left, collapsed: false);
        var changed = new List<string?>();
        bar.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        bar.IsCollapsed = true;

        Assert.Contains(nameof(NavigationViewModel.ChevronPointing), changed);
        Assert.Contains(nameof(NavigationViewModel.ChevronIcon), changed);
    }

    [Fact]
    public void Redocking_RaisesChangeNotificationsForTheWholePuck() {
        var bar = Bar(NavOrientation.Left, collapsed: false);
        var changed = new List<string?>();
        bar.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        bar.Orientation = NavOrientation.Top;

        Assert.Contains(nameof(NavigationViewModel.ChevronPointing), changed);
        Assert.Contains(nameof(NavigationViewModel.ChevronIcon), changed);
        Assert.Contains(nameof(NavigationViewModel.ChevronWidth), changed);
        Assert.Contains(nameof(NavigationViewModel.ChevronHeight), changed);
        Assert.Contains(nameof(NavigationViewModel.ChevronHAlign), changed);
        Assert.Contains(nameof(NavigationViewModel.ChevronVAlign), changed);
        Assert.Contains(nameof(NavigationViewModel.ChevronMargin), changed);
        Assert.Contains(nameof(NavigationViewModel.ChevronCornerRadius), changed);
    }
}
