using DashDetective.Shell.Navigation;
using DashDetective.Tests.Services.Theming;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
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

    // A report is stale once the bar's labels change height, but only a bar that is not horizontal
    // needs telling: a horizontal one reports its new height itself.
    [Fact]
    public void SetTextScale_WhileVertical_DiscardsTheMeasuredHeight() {
        var bar = Bar(NavOrientation.Top, collapsed: false);
        bar.ReportBarHeight(47);
        bar.Orientation = NavOrientation.Left;
        bar.SetTextScale(1.5);

        Assert.Equal(NavigationViewModel.HorizontalBarEstimate, bar.RailThickness(horizontal: true));
    }

    // A horizontal bar with a scroll bar is taller than the estimate and may not change height with the
    // text, so throwing its report away would leave nothing to correct the preview.
    [Fact]
    public void SetTextScale_WhileHorizontal_KeepsTheMeasuredHeight() {
        var bar = Bar(NavOrientation.Top, collapsed: true);
        bar.ReportBarHeight(57);
        bar.SetTextScale(0.8);

        Assert.Equal(57, bar.RailThickness(horizontal: true));
    }

    [Fact]
    public void Collapsing_WhileVertical_DiscardsTheMeasuredHeight() {
        var bar = Bar(NavOrientation.Top, collapsed: false);
        bar.ReportBarHeight(58);
        bar.Orientation = NavOrientation.Left;
        bar.IsCollapsed = true;

        Assert.Equal(NavigationViewModel.HorizontalBarEstimate, bar.RailThickness(horizontal: true));
    }

    [Fact]
    public void AutoCollapsing_WhileVertical_DiscardsTheMeasuredHeight() {
        var bar = Bar(NavOrientation.Top, collapsed: false);
        bar.ReportBarHeight(58);
        bar.Orientation = NavOrientation.Left;
        bar.SetShellWidth(NavigationViewModel.AutoCollapseWidth - 1);

        Assert.Equal(NavigationViewModel.HorizontalBarEstimate, bar.RailThickness(horizontal: true));
    }

    [Fact]
    public void UiScale_FollowsTheInterfaceSize_ForTheDropBand() {
        var bar = Bar(NavOrientation.Left, collapsed: false);
        Assert.Equal(1, bar.UiScale);

        bar.SetUiScale(1.5);

        Assert.Equal(1.5, bar.UiScale);
    }

    /// <summary>The fix itself lives in markup, where a view-model test cannot see it: a Height on the rail
    /// brings the fixed bar back, and an inline Padding on the footer silently beats the horizontal
    /// style that keeps it to the item rows' height.</summary>
    [Fact]
    public void Markup_LeavesTheBarHeightToItsContent() {
        var file = Path.Combine(PaletteFile.SourceRoot(), "src/Shell/Navigation/NavigationView.axaml");
        var root = XDocument.Load(file).Root!;

        var rail = root.Descendants().Single(e => (string?)e.Attribute(X + "Name") == "RailBorder");
        Assert.Null(rail.Attribute("Height"));

        var footer = root.Descendants().Single(e => e.Name.LocalName == "Border" && (string?)e.Attribute("Classes") == "footer");
        Assert.Null(footer.Attribute("Padding"));
    }

    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";

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
