using DashDetective.Tabs.Processes;
using Xunit;

namespace DashDetective.Tests.Tabs.Processes;

/// <summary>Covers <see cref="ProcessMemoryFormatter.Format"/>: the always-MB, whole-number readout,
/// the non-positive floor, and sub-MB rounding.</summary>
public class ProcessMemoryFormatterTests {
    private const long Mb = 1024 * 1024;

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Format_NonPositive_ShowsZeroMegabytes(long bytes) {
        Assert.Equal("0 MB", ProcessMemoryFormatter.Format(bytes));
    }

    [Fact]
    public void Format_WholeMegabytes_RendersInMb() {
        Assert.Equal("412 MB", ProcessMemoryFormatter.Format(412 * Mb));
    }

    [Fact]
    public void Format_LargeValue_StaysInMb() {
        Assert.Equal("2048 MB", ProcessMemoryFormatter.Format(2048 * Mb));
    }

    [Theory]
    [InlineData(419430, "0 MB")]   // ≈ 0.4 MB → rounds down
    [InlineData(629145, "1 MB")]   // ≈ 0.6 MB → rounds up
    public void Format_SubMegabyte_RoundsToNearestWhole(long bytes, string expected) {
        Assert.Equal(expected, ProcessMemoryFormatter.Format(bytes));
    }
}
