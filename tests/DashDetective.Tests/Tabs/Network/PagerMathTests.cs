using DashDetective.Tabs.Network;
using Xunit;

namespace DashDetective.Tests.Tabs.Network;

/// <summary>Covers <see cref="PagerMath"/>: page-count ceiling (empty → one page), page clamping,
/// slice start, and the last-page / past-the-end slice count. Page size is the table's fixed 100.</summary>
public class PagerMathTests {
    private const int PageSize = 100;

    [Theory]
    [InlineData(0, 1)]     // empty list still occupies one page
    [InlineData(1, 1)]
    [InlineData(100, 1)]
    [InlineData(101, 2)]
    [InlineData(200, 2)]
    [InlineData(250, 3)]
    public void PageCount_CeilingNeverBelowOne(int available, int expected) {
        Assert.Equal(expected, PagerMath.PageCount(available, PageSize));
    }

    [Theory]
    [InlineData(0, 5, 1)]    // below 1 → 1
    [InlineData(-3, 5, 1)]
    [InlineData(3, 5, 3)]
    [InlineData(9, 5, 5)]    // above count → clamped down to the last page
    [InlineData(1, 1, 1)]
    public void ClampPage_ClampsIntoRange(int page, int pageCount, int expected) {
        Assert.Equal(expected, PagerMath.ClampPage(page, pageCount));
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(3, 200)]
    public void PageStart_IsZeroBasedOffset(int page, int expected) {
        Assert.Equal(expected, PagerMath.PageStart(page, PageSize));
    }

    [Theory]
    [InlineData(250, 0, 100)]     // full first page
    [InlineData(250, 200, 50)]    // last-page remainder
    [InlineData(250, 300, 0)]     // start past the end → empty slice
    [InlineData(0, 0, 0)]
    public void SliceCount_FullRemainderOrEmpty(int available, int start, int expected) {
        Assert.Equal(expected, PagerMath.SliceCount(available, start, PageSize));
    }

    [Fact]
    public void LastPage_StartAndCount_ComposeToRemainder() {
        // 250 items, page 3: starts at index 200, holds the final 50.
        var start = PagerMath.PageStart(3, PageSize);
        Assert.Equal(200, start);
        Assert.Equal(50, PagerMath.SliceCount(250, start, PageSize));
    }
}
