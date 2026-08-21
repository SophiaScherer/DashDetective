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
}
