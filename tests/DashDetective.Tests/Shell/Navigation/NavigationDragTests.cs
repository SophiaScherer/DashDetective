using DashDetective.Shell.Navigation;
using System.Collections.Generic;
using Xunit;

namespace DashDetective.Tests.Shell.Navigation;

/// <summary>Covers the view-model side of drag-to-dock: the flag that dims the bar in place, and the
/// rail thickness the drop preview measures itself against. The gesture itself lives in the view's
/// code-behind.</summary>
public class NavigationDragTests {
    private static NavigationViewModel Bar(NavOrientation orientation, bool collapsed) =>
        new() { Orientation = orientation, IsCollapsed = collapsed };

    [Fact]
    public void IsDragging_DefaultsToFalse() {
        Assert.False(new NavigationViewModel().IsDragging);
    }

    [Fact]
    public void IsDragging_RaisesChangeNotification_SoTheDimCanStyleOnIt() {
        var bar = new NavigationViewModel();
        var changed = new List<string?>();
        bar.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        bar.IsDragging = true;

        Assert.Contains(nameof(NavigationViewModel.IsDragging), changed);
    }

    // The drop band is sized from RailThickness, so it has to agree with the rail's own dimensions —
    // otherwise the preview would show the bar landing somewhere it does not.
    [Theory]
    [InlineData(NavOrientation.Left, false)]
    [InlineData(NavOrientation.Left, true)]
    [InlineData(NavOrientation.Right, false)]
    [InlineData(NavOrientation.Right, true)]
    public void RailThickness_VerticalRail_MatchesRailWidth(NavOrientation orientation, bool collapsed) {
        var bar = Bar(orientation, collapsed);
        Assert.Equal(bar.RailWidth, bar.RailThickness(horizontal: false));
    }

    [Theory]
    [InlineData(NavOrientation.Top, false)]
    [InlineData(NavOrientation.Top, true)]
    [InlineData(NavOrientation.Bottom, false)]
    [InlineData(NavOrientation.Bottom, true)]
    public void RailThickness_HorizontalBar_MatchesRailHeight(NavOrientation orientation, bool collapsed) {
        var bar = Bar(orientation, collapsed);
        Assert.Equal(bar.RailHeight, bar.RailThickness(horizontal: true));
    }

    // Dragging previews edges the bar is not docked to, so both axes must answer whichever edge it is on.
    [Theory]
    [InlineData(NavOrientation.Left)]
    [InlineData(NavOrientation.Top)]
    public void RailThickness_AnswersBothAxes_WhicheverEdgeTheBarIsOn(NavOrientation orientation) {
        var bar = Bar(orientation, collapsed: false);

        Assert.Equal(236, bar.RailThickness(horizontal: false));
        Assert.Equal(64, bar.RailThickness(horizontal: true));
    }

    [Fact]
    public void RailThickness_CollapsedBar_ReportsTheNarrowerBand() {
        var bar = Bar(NavOrientation.Left, collapsed: true);

        Assert.Equal(64, bar.RailThickness(horizontal: false));
        Assert.Equal(54, bar.RailThickness(horizontal: true));
    }
}
