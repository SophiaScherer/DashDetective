using DashDetective.Shared.Charts;
using Xunit;

namespace DashDetective.Tests.Shared.Charts;

/// <summary>
/// Covers <see cref="ChartAxis"/>: how much room a chart's axis text takes and what an auto-scaled axis
/// says. The reservation rules are the load-bearing half — a chart with no labels must measure exactly as
/// it did before them, since every stat-card mini and per-core cell is one.
/// </summary>
public class ChartAxisTests {
    [Fact]
    public void Gutter_NoLabels_ReservesNothing() {
        Assert.Equal(0, ChartAxis.Gutter(0, 0, 0));
    }

    [Fact]
    public void Gutter_FitsTheWidestLabel() {
        Assert.Equal(30 + ChartAxis.LabelGap, ChartAxis.Gutter(12, 30, 8));
    }

    [Fact]
    public void Footer_NoLabels_ReservesNothing() {
        Assert.Equal(0, ChartAxis.Footer(0));
    }

    [Fact]
    public void Footer_FitsTheTextPlusItsGap() {
        Assert.Equal(11 + ChartAxis.FooterGap, ChartAxis.Footer(11));
    }

    /// <summary>The regression this whole split exists to prevent: an unlabelled chart draws over its whole
    /// self, exactly as it did before the axis furniture was added.</summary>
    [Fact]
    public void PlotRect_NoReservations_IsTheWholeControl() {
        var plot = ChartAxis.PlotRect(200, 80, gutter: 0, footer: 0);

        Assert.Equal(0, plot.Left);
        Assert.Equal(0, plot.Top);
        Assert.Equal(200, plot.Width);
        Assert.Equal(80, plot.Height);
    }

    [Fact]
    public void PlotRect_TakesTheGutterOffTheLeftAndTheFooterOffTheBottom() {
        var plot = ChartAxis.PlotRect(200, 80, gutter: 30, footer: 14);

        Assert.Equal(30, plot.Left);
        Assert.Equal(170, plot.Width);
        Assert.Equal(66, plot.Height);
        Assert.Equal(0, plot.Top);
    }

    /// <summary>A control too small for its own labels must give the reservation up rather than hand back a
    /// negative size, which would throw as a Rect and take the whole page's render down with it.</summary>
    [Theory]
    [InlineData(20, 10, 30, 14)]
    [InlineData(0, 0, 30, 14)]
    [InlineData(5, 5, 500, 500)]
    public void PlotRect_TooSmallForItsLabels_StaysPositive(double w, double h, double gutter, double footer) {
        var plot = ChartAxis.PlotRect(w, h, gutter, footer);

        Assert.True(plot.Width >= ChartAxis.MinPlot);
        Assert.True(plot.Height >= ChartAxis.MinPlot);
        Assert.True(plot.Left >= 0);
    }

    /// <summary>The unit rides on the top label only — repeating it halfway down would widen the gutter for
    /// nothing — and the axis reads in the same unit as the readouts beside it.</summary>
    [Theory]
    [InlineData(80, "80 Mbps", "40")]
    [InlineData(0.4, "400 kbps", "200")]
    [InlineData(2400, "2.4 Gbps", "1.2")]
    public void RateLabels_ScaleToOneUnitTakenFromTheCeiling(double axisMax, string top, string middle) {
        var (actualTop, actualMiddle, bottom) = ChartAxis.RateLabels(axisMax);

        Assert.Equal(top, actualTop);
        Assert.Equal(middle, actualMiddle);
        Assert.Equal("0", bottom);
    }

    /// <summary>The same rule over more bands, so a chart with room to label every grid line reads in one
    /// unit taken from its ceiling.</summary>
    [Fact]
    public void RateLabels_OverFourBands_LabelsEveryGridLine() {
        Assert.Equal(new[] { "80 Mbps", "60", "40", "20", "0" }, ChartAxis.RateLabels(80, 4));
    }

    /// <summary>The percent sign stays on every reading but the zero: it matches the fixed labels the other
    /// tabs' charts carry, and costs no gutter because the top label is the widest either way.</summary>
    [Fact]
    public void PercentLabels_LabelsEveryBandBoundaryTopToBottom() {
        Assert.Equal(new[] { "100%", "75%", "50%", "25%", "0" }, ChartAxis.PercentLabels(4));
    }

    /// <summary>One band is the ends alone — what a per-core cell carries, being too short for more.</summary>
    [Fact]
    public void PercentLabels_OneBand_IsTheEndsAlone() {
        Assert.Equal(new[] { "100%", "0" }, ChartAxis.PercentLabels(1));
    }

    [Fact]
    public void FitLabelCount_PlotWithRoomForThemAll_KeepsThemAll() {
        Assert.Equal(5, ChartAxis.FitLabelCount(plotExtent: 200, labelExtent: 12, desired: 5));
    }

    /// <summary>A sparser set has to land on the same grid lines, so five drops to three rather than four —
    /// four would sit between the lines it is meant to be reading.</summary>
    [Fact]
    public void FitLabelCount_TooTight_DropsToASetThatStillLandsOnGridLines() {
        Assert.Equal(3, ChartAxis.FitLabelCount(plotExtent: 50, labelExtent: 12, desired: 5));
    }

    /// <summary>The two ends are the floor: a chart saying nothing about its scale is worse than a crowded
    /// one, and the ends are what a reading is measured between.</summary>
    [Fact]
    public void FitLabelCount_NoRoomAtAll_KeepsTheTwoEnds() {
        Assert.Equal(2, ChartAxis.FitLabelCount(plotExtent: 8, labelExtent: 12, desired: 5));
    }

    [Fact]
    public void FitLabelCount_TwoOrFewer_IsAlreadyTheFloor() {
        Assert.Equal(2, ChartAxis.FitLabelCount(plotExtent: 8, labelExtent: 12, desired: 2));
    }

    /// <summary>Grid lines land on the half-pixel centre of a device pixel, so a 1px line draws crisp
    /// rather than smeared across two.</summary>
    [Theory]
    [InlineData(0, 0.5)]
    [InlineData(20, 20.5)]
    [InlineData(20.4, 20.5)]
    [InlineData(20.6, 21.5)]
    public void GridLine_SnapsToTheHalfPixel(double value, double expected) {
        Assert.Equal(expected, ChartAxis.GridLine(value, 0, 80));
    }

    /// <summary>The regression: the last line of each run sat at the plot's exact edge, which draws half
    /// outside a chart that does not clip — the grid bled into the padding of the card hosting it.</summary>
    [Fact]
    public void GridLine_HoldsTheOutermostLinesInsideThePlot() {
        Assert.Equal(0.5, ChartAxis.GridLine(0, 0, 80));
        Assert.Equal(79.5, ChartAxis.GridLine(80, 0, 80));
    }

    [Fact]
    public void GridLine_RespectsAnOffsetPlot() {
        Assert.Equal(30.5, ChartAxis.GridLine(30, 30, 200));
        Assert.Equal(199.5, ChartAxis.GridLine(200, 30, 200));
    }

    /// <summary>A plot too thin to hold both edges keeps the near one rather than inverting, which would
    /// throw out of Math.Clamp and take the whole page's render with it.</summary>
    [Fact]
    public void GridLine_PlotThinnerThanOnePixel_StaysPositive() {
        Assert.Equal(0.5, ChartAxis.GridLine(0.4, 0, 0.8));
    }
}
