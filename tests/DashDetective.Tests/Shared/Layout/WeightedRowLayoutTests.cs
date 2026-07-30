using DashDetective.Shared.Layout;
using Xunit;

namespace DashDetective.Tests.Shared.Layout;

/// <summary>Covers <see cref="WeightedRowLayout"/>: the stack threshold (including which child sets
/// it) and the proportional split, which must leave no sub-pixel seam.</summary>
public class WeightedRowLayoutTests {
    [Fact]
    public void RequiredWidth_EqualWeights_IsSumOfMinimums() {
        // The Dashboard's two utilization panels: equal halves, 340 each.
        var need = WeightedRowLayout.RequiredWidth(new double[] { 1, 1 }, new double[] { 340, 340 });
        Assert.Equal(680, need, 6);
    }

    [Fact]
    public void RequiredWidth_UnequalWeights_DrivenByWorstRatio() {
        // 420/1.35 = 311.1 beats 300/1 = 300, so the wide panel sets the requirement: 2.35 · 311.1.
        var need = WeightedRowLayout.RequiredWidth(new double[] { 1.35, 1 }, new double[] { 420, 300 });
        Assert.Equal(731.111111, need, 5);
    }

    [Fact]
    public void RequiredWidth_NarrowChildSetsIt_WhenItsRatioIsWorse() {
        // 300/1 = 300 beats 380/1.35 = 281.5, so here the narrow panel governs.
        var need = WeightedRowLayout.RequiredWidth(new double[] { 1.35, 1 }, new double[] { 380, 300 });
        Assert.Equal(705, need, 6);
    }

    [Fact]
    public void RequiredWidth_NoMinimums_IsZero() {
        // Nothing to honour means the row never has to stack.
        Assert.Equal(0, WeightedRowLayout.RequiredWidth(new double[] { 1, 2 }, new double[] { 0, 0 }), 6);
    }

    [Fact]
    public void RequiredWidth_ZeroWeight_Ignored() {
        // A zero-weight child gets no slice, so its minimum cannot force a stack.
        var need = WeightedRowLayout.RequiredWidth(new double[] { 1, 0 }, new double[] { 100, 5000 });
        Assert.Equal(100, need, 6);
    }

    [Fact]
    public void RequiredWidth_ThreeChildren_UsesTheWorstOfAll() {
        // Network row 1: 260/1, 260/1, 280/1.1 = 254.5 → worst is 260, total weight 3.1.
        var need = WeightedRowLayout.RequiredWidth(new double[] { 1, 1, 1.1 },
                                                   new double[] { 260, 260, 280 });
        Assert.Equal(806, need, 6);
    }

    [Fact]
    public void Split_ProportionalSlices() {
        var widths = new double[2];
        WeightedRowLayout.Split(1300, new double[] { 1.6, 1 }, widths);
        Assert.Equal(800, widths[0], 6);
        Assert.Equal(500, widths[1], 6);
    }

    [Fact]
    public void Split_SumsExactlyToContentWidth() {
        // A ratio that does not divide cleanly still has to fill the row exactly.
        var widths = new double[3];
        WeightedRowLayout.Split(1000, new double[] { 1, 1, 1 }, widths);
        Assert.Equal(1000, widths[0] + widths[1] + widths[2], 10);
    }

    [Fact]
    public void Split_ZeroWeightChild_GetsNothing() {
        var widths = new double[3];
        WeightedRowLayout.Split(900, new double[] { 2, 0, 1 }, widths);
        Assert.Equal(600, widths[0], 6);
        Assert.Equal(0, widths[1], 6);
        Assert.Equal(300, widths[2], 6);
    }

    [Fact]
    public void Split_ZeroTotalWeight_DividesEvenly() {
        var widths = new double[4];
        WeightedRowLayout.Split(800, new double[] { 0, 0, 0, 0 }, widths);
        foreach (var w in widths)
            Assert.Equal(200, w, 6);
    }

    [Fact]
    public void Split_SingleChild_TakesTheWholeRow() {
        var widths = new double[1];
        WeightedRowLayout.Split(640, new double[] { 1.6 }, widths);
        Assert.Equal(640, widths[0], 6);
    }
}
