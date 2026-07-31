using DashDetective.Shared.Charts;
using Xunit;

namespace DashDetective.Tests.Shared.Charts;

/// <summary>Covers <see cref="ChartAspect"/>: the ratio itself, both height clamps, and the
/// degenerate inputs a layout pass really produces (zero and infinite width).</summary>
public class ChartAspectTests {
    // The "chartPanel" shape from src/Shared/Styles/Layout.axaml, used throughout as a realistic case.
    private const double Ratio = 3.2;
    private const double Min = 90;
    private const double Max = 220;

    [Fact]
    public void HeightForWidth_WithinBounds_DividesWidthByRatio() {
        Assert.Equal(128.125, ChartAspect.HeightForWidth(410, Ratio, Min, Max), 6);
    }

    [Fact]
    public void HeightForWidth_WideSlot_ClampsToMaxHeight() {
        // 1000 / 3.2 = 312.5, above the cap.
        Assert.Equal(Max, ChartAspect.HeightForWidth(1000, Ratio, Min, Max), 6);
    }

    [Fact]
    public void HeightForWidth_NarrowSlot_ClampsToMinHeight() {
        // 200 / 3.2 = 62.5, below the floor.
        Assert.Equal(Min, ChartAspect.HeightForWidth(200, Ratio, Min, Max), 6);
    }

    [Fact]
    public void HeightForWidth_UnboundedMax_ScalesLinearly() {
        Assert.Equal(128.125, ChartAspect.HeightForWidth(410, Ratio), 6);
        Assert.Equal(256.25, ChartAspect.HeightForWidth(820, Ratio), 6);
    }

    [Fact]
    public void HeightForWidth_InfiniteWidth_FallsBackToMaxHeight() {
        Assert.Equal(Max, ChartAspect.HeightForWidth(double.PositiveInfinity, Ratio, Min, Max), 6);
    }

    [Fact]
    public void HeightForWidth_InfiniteWidthAndUnboundedMax_FallsBackToMinHeight() {
        Assert.Equal(Min, ChartAspect.HeightForWidth(double.PositiveInfinity, Ratio, Min), 6);
    }

    [Fact]
    public void HeightForWidth_NaNWidth_FallsBackToMaxHeight() {
        Assert.Equal(Max, ChartAspect.HeightForWidth(double.NaN, Ratio, Min, Max), 6);
    }

    [Fact]
    public void HeightForWidth_ZeroWidth_ClampsToMinHeight() {
        // The first layout pass can hand out a zero width; the floor keeps the chart from vanishing.
        Assert.Equal(Min, ChartAspect.HeightForWidth(0, Ratio, Min, Max), 6);
    }

    [Fact]
    public void HeightForWidth_NonPositiveRatio_FallsBackToMaxHeight() {
        Assert.Equal(Max, ChartAspect.HeightForWidth(410, 0, Min, Max), 6);
        Assert.Equal(Max, ChartAspect.HeightForWidth(410, -2, Min, Max), 6);
    }

    [Fact]
    public void HeightForWidth_NaNRatio_FallsBackToMaxHeight() {
        // NaN is the property default, i.e. aspect sizing switched off.
        Assert.Equal(Max, ChartAspect.HeightForWidth(410, double.NaN, Min, Max), 6);
    }

    [Fact]
    public void HeightForWidth_MinAboveMax_PrefersMin() {
        // Matches Avalonia's MinMax clamp order, so the control and the layout system agree.
        Assert.Equal(300, ChartAspect.HeightForWidth(410, Ratio, 300, 220), 6);
    }

    [Fact]
    public void HeightForWidth_DefaultBounds_AreUnconstrained() {
        Assert.Equal(100, ChartAspect.HeightForWidth(420, 4.2), 6);
    }
}
