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

    // A horizontal bar sizes to its content, so the band is whatever the layout last reported.
    [Theory]
    [InlineData(NavOrientation.Top, false)]
    [InlineData(NavOrientation.Top, true)]
    [InlineData(NavOrientation.Bottom, false)]
    [InlineData(NavOrientation.Bottom, true)]
    public void RailThickness_HorizontalBar_MatchesTheReportedHeight(NavOrientation orientation, bool collapsed) {
        var bar = Bar(orientation, collapsed);
        bar.ReportBarHeight(47);

        Assert.Equal(47, bar.RailThickness(horizontal: true));
    }

    [Fact]
    public void RailThickness_HorizontalBarNotYetLaidOut_FallsBackToTheEstimate() {
        var bar = Bar(NavOrientation.Left, collapsed: false);
        Assert.Equal(NavigationViewModel.HorizontalBarEstimate, bar.RailThickness(horizontal: true));
    }

    // A vertical rail's width is not a horizontal bar's height, so a report from one must not size the other.
    [Theory]
    [InlineData(NavOrientation.Left)]
    [InlineData(NavOrientation.Right)]
    public void ReportBarHeight_VerticalRail_IsIgnored(NavOrientation orientation) {
        var bar = Bar(orientation, collapsed: false);
        bar.ReportBarHeight(700);

        Assert.Equal(NavigationViewModel.HorizontalBarEstimate, bar.RailThickness(horizontal: true));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void ReportBarHeight_UnusableHeight_KeepsTheLastGoodOne(double height) {
        var bar = Bar(NavOrientation.Top, collapsed: false);
        bar.ReportBarHeight(47);
        bar.ReportBarHeight(height);

        Assert.Equal(47, bar.RailThickness(horizontal: true));
    }

    /// <summary>The bug this pins: the bar's height was its authored height times the text scale, but only
    /// the labels grow, so at 200% it drew an empty band as tall as its content.</summary>
    [Fact]
    public void RailThickness_HorizontalBarAtLargeText_IsWhatTheContentNeeds() {
        var bar = Bar(NavOrientation.Top, collapsed: false);
        bar.SetTextScale(2);
        bar.ReportBarHeight(58);

        Assert.Equal(58, bar.RailThickness(horizontal: true));
    }

    // The labels change height with the text, so a height measured at the old size is stale.
    [Fact]
    public void SetTextScale_DiscardsTheMeasuredHeight() {
        var bar = Bar(NavOrientation.Top, collapsed: false);
        bar.ReportBarHeight(47);
        bar.SetTextScale(1.5);

        Assert.Equal(NavigationViewModel.HorizontalBarEstimate, bar.RailThickness(horizontal: true));
    }

    // Dragging previews edges the bar is not docked to, so both axes must answer whichever edge it is on.
    [Theory]
    [InlineData(NavOrientation.Left)]
    [InlineData(NavOrientation.Top)]
    public void RailThickness_AnswersBothAxes_WhicheverEdgeTheBarIsOn(NavOrientation orientation) {
        var bar = Bar(orientation, collapsed: false);

        Assert.Equal(236, bar.RailThickness(horizontal: false));
        Assert.Equal(NavigationViewModel.HorizontalBarEstimate, bar.RailThickness(horizontal: true));
    }

    [Fact]
    public void RailThickness_CollapsedBar_ReportsTheNarrowerBand() {
        var bar = Bar(NavOrientation.Left, collapsed: true);

        Assert.Equal(64, bar.RailThickness(horizontal: false));
    }

    /// <summary>The rail grows with the text it holds, but its icons do not shrink with smaller text, so
    /// below 100% it keeps the width they were drawn for — narrower, a collapsed rail's scroll bar
    /// covered half of every icon.</summary>
    [Fact]
    public void RailThickness_TextBelowOneHundred_KeepsTheAuthoredWidth() {
        var bar = Bar(NavOrientation.Left, collapsed: true);
        bar.SetTextScale(0.8);

        Assert.Equal(64, bar.RailThickness(horizontal: false));
    }

    [Fact]
    public void RailThickness_TextAboveOneHundred_GrowsWithIt() {
        var bar = Bar(NavOrientation.Left, collapsed: false);
        bar.SetTextScale(2);

        Assert.Equal(472, bar.RailThickness(horizontal: false));
    }
}
