using DashDetective.Shared.Layout;
using Xunit;

namespace DashDetective.Tests.Shared.Layout;

/// <summary>Covers <see cref="FlowLayout"/>: the wrap threshold (including the exact-fit boundary),
/// the item-count and MaxColumns ceilings, and the gutter subtraction.</summary>
public class FlowLayoutTests {
    // The Dashboard stat row's numbers: five 168px cards with 12px gutters.
    private const double Min = 160;
    private const double Spacing = 12;

    [Fact]
    public void ColumnCount_ExactFit_IncludesTheLastColumn() {
        // 5·160 + 4·12 = 848, exactly enough for five.
        Assert.Equal(5, FlowLayout.ColumnCount(848, Min, Spacing, 5));
    }

    [Fact]
    public void ColumnCount_OnePixelShort_DropsAColumn() {
        Assert.Equal(4, FlowLayout.ColumnCount(847, Min, Spacing, 5));
    }

    [Fact]
    public void ColumnCount_WideRow_CapsAtItemCount() {
        Assert.Equal(3, FlowLayout.ColumnCount(5000, Min, Spacing, 3));
    }

    [Fact]
    public void ColumnCount_MaxColumnsSet_Caps() {
        // Room for plenty, but the design says never more than five across.
        Assert.Equal(5, FlowLayout.ColumnCount(5000, Min, Spacing, 12, maxColumns: 5));
    }

    [Fact]
    public void ColumnCount_MaxColumnsAboveItemCount_UsesItemCount() {
        Assert.Equal(2, FlowLayout.ColumnCount(5000, Min, Spacing, 2, maxColumns: 5));
    }

    [Fact]
    public void ColumnCount_NarrowerThanOneItem_ReturnsOne() {
        Assert.Equal(1, FlowLayout.ColumnCount(80, Min, Spacing, 5));
    }

    [Fact]
    public void ColumnCount_NoItems_ReturnsOne() {
        // Never zero: callers divide by the result.
        Assert.Equal(1, FlowLayout.ColumnCount(848, Min, Spacing, 0));
    }

    [Fact]
    public void ColumnCount_InfiniteWidth_ReturnsMaximum() {
        // An Auto slot imposes no wrap, so the ceiling applies unchanged.
        Assert.Equal(5, FlowLayout.ColumnCount(double.PositiveInfinity, Min, Spacing, 12, maxColumns: 5));
    }

    [Fact]
    public void ColumnCount_ZeroMinWidth_ReturnsItemCount() {
        // No minimum means nothing ever forces a wrap.
        Assert.Equal(6, FlowLayout.ColumnCount(100, 0, Spacing, 6));
    }

    [Fact]
    public void ColumnCount_NoSpacing_FitsByWidthAlone() {
        Assert.Equal(4, FlowLayout.ColumnCount(640, Min, 0, 10));
    }

    [Fact]
    public void ColumnCount_SubPixelShortOfExactFit_StillFits() {
        // Layout arithmetic can land a hair under; that must not cost a column.
        Assert.Equal(5, FlowLayout.ColumnCount(848 - 1e-9, Min, Spacing, 5));
    }

    [Fact]
    public void ItemWidth_SubtractsGutters() {
        // 848 − 4·12 = 800, split five ways.
        Assert.Equal(160, FlowLayout.ItemWidth(848, 5, Spacing), 6);
    }

    [Fact]
    public void ItemWidth_SingleColumn_TakesFullWidth() {
        Assert.Equal(400, FlowLayout.ItemWidth(400, 1, Spacing), 6);
    }

    [Fact]
    public void ItemWidth_GuttersExceedWidth_ReturnsZero() {
        Assert.Equal(0, FlowLayout.ItemWidth(20, 5, Spacing), 6);
    }

    [Fact]
    public void ItemWidth_InfiniteWidth_ReturnsZero() {
        Assert.Equal(0, FlowLayout.ItemWidth(double.PositiveInfinity, 3, Spacing), 6);
    }

    [Fact]
    public void ItemWidth_NoColumns_ReturnsZero() {
        Assert.Equal(0, FlowLayout.ItemWidth(848, 0, Spacing), 6);
    }
}
