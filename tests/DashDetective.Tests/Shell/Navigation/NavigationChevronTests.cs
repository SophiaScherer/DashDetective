using Avalonia;
using Avalonia.Layout;
using DashDetective.Shell.Navigation;
using System.Collections.Generic;
using Xunit;

namespace DashDetective.Tests.Shell.Navigation;

/// <summary>Covers the collapse/expand puck — the semi-circle domed into the bar, its flat side lying on
/// the content-facing edge. Its chevron must point the way the bar will move, and its size, alignment and
/// rounding must all follow the docked edge (all computed on the view model, no converters). The reveal
/// itself lives in <see cref="NavigationChevronRevealTests"/>.</summary>
public class NavigationChevronTests {
    // The half-disc's radius: how far it reaches into the bar, and half its flat side.
    private const double Radius = 20;

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

    // A half-disc is one radius deep and two long, whichever edge it sits on.
    [Theory]
    [InlineData(NavOrientation.Left)]
    [InlineData(NavOrientation.Right)]
    public void ChevronSize_VerticalRail_IsOneRadiusWideAndTwoTall(NavOrientation orientation) {
        var bar = Bar(orientation, collapsed: false);
        Assert.Equal(Radius, bar.ChevronWidth);
        Assert.Equal(Radius * 2, bar.ChevronHeight);
    }

    [Theory]
    [InlineData(NavOrientation.Top)]
    [InlineData(NavOrientation.Bottom)]
    public void ChevronSize_HorizontalBar_IsTwoRadiiWideAndOneTall(NavOrientation orientation) {
        var bar = Bar(orientation, collapsed: false);
        Assert.Equal(Radius * 2, bar.ChevronWidth);
        Assert.Equal(Radius, bar.ChevronHeight);
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

    // Both corners facing INTO the bar rounded by the full radius: on a box one radius deep and two long,
    // that is exactly a half-disc, domed inward with its flat side on the content-facing edge.
    [Theory]
    [InlineData(NavOrientation.Left, Radius, 0d, 0d, Radius)]
    [InlineData(NavOrientation.Right, 0d, Radius, Radius, 0d)]
    [InlineData(NavOrientation.Top, Radius, Radius, 0d, 0d)]
    [InlineData(NavOrientation.Bottom, 0d, 0d, Radius, Radius)]
    public void ChevronCornerRadius_RoundsOnlyTheInwardCorners(
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
        Assert.Contains(nameof(NavigationViewModel.ChevronCornerRadius), changed);
    }
}
