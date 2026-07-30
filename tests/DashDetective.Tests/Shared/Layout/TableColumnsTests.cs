using DashDetective.Shared.Layout;
using Xunit;

namespace DashDetective.Tests.Shared.Layout;

/// <summary>Covers <see cref="TableColumns"/>: dropping from the end of the drop order, the required
/// floor, and the exact-fit boundary.</summary>
public class TableColumnsTests {
    // Four columns needing 100/80/60/40, with an 8px gap: 280 + 24 = 304 to show them all.
    private static readonly double[] Mins = { 100, 80, 60, 40 };
    private const double Spacing = 8;

    [Fact]
    public void VisibleColumns_ExactFit_KeepsThemAll() {
        Assert.Equal(4, TableColumns.VisibleColumns(304, Mins, Spacing));
    }

    [Fact]
    public void VisibleColumns_OnePixelShort_DropsTheLast() {
        // 240 + 16 = 256 for three, which 303 clears.
        Assert.Equal(3, TableColumns.VisibleColumns(303, Mins, Spacing));
    }

    [Fact]
    public void VisibleColumns_Narrower_DropsFromTheEndInOrder() {
        Assert.Equal(2, TableColumns.VisibleColumns(255, Mins, Spacing));
        Assert.Equal(1, TableColumns.VisibleColumns(187, Mins, Spacing));
    }

    [Fact]
    public void VisibleColumns_NeverBelowRequired() {
        Assert.Equal(3, TableColumns.VisibleColumns(10, Mins, Spacing, required: 3));
    }

    [Fact]
    public void VisibleColumns_RequiredAboveCount_ClampsToCount() {
        Assert.Equal(4, TableColumns.VisibleColumns(10, Mins, Spacing, required: 9));
    }

    [Fact]
    public void VisibleColumns_InfiniteWidth_KeepsThemAll() {
        Assert.Equal(4, TableColumns.VisibleColumns(double.PositiveInfinity, Mins, Spacing));
    }

    [Fact]
    public void VisibleColumns_WideTable_KeepsThemAll() {
        Assert.Equal(4, TableColumns.VisibleColumns(5000, Mins, Spacing));
    }
}
