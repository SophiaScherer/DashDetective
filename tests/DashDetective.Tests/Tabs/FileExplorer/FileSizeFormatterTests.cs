using DashDetective.Tabs.FileExplorer;
using Xunit;

namespace DashDetective.Tests.Tabs.FileExplorer;

/// <summary>Covers <see cref="FileSizeFormatter.Format"/>: the B/KB/MB/GB/TB rollovers (power-of-two,
/// inclusive thresholds), whole vs one-decimal rendering, and the negative clamp.</summary>
public class FileSizeFormatterTests {
    private const long Kb = 1024;
    private const long Mb = Kb * 1024;
    private const long Gb = Mb * 1024;
    private const long Tb = Gb * 1024;

    [Fact]
    public void Format_Bytes_ShowsRawWithBSuffix() {
        Assert.Equal("512 B", FileSizeFormatter.Format(512));
    }

    [Fact]
    public void Format_Zero_ShowsZeroBytes() {
        Assert.Equal("0 B", FileSizeFormatter.Format(0));
    }

    [Theory]
    [InlineData(1024, "1 KB")]         // exactly 1 KB (inclusive threshold)
    [InlineData(2048, "2 KB")]
    public void Format_Kilobytes_RendersWhole(long bytes, string expected) {
        Assert.Equal(expected, FileSizeFormatter.Format(bytes));
    }

    [Fact]
    public void Format_Megabytes_KeepOneDecimalDroppingTrailingZero() {
        Assert.Equal("1 MB", FileSizeFormatter.Format(Mb));            // 1.0 → ".0" dropped
        Assert.Equal("1.5 MB", FileSizeFormatter.Format(Mb + Mb / 2));
    }

    [Fact]
    public void Format_Gigabytes_RendersWithGbSuffix() {
        Assert.Equal("2 GB", FileSizeFormatter.Format(2 * Gb));
    }

    [Fact]
    public void Format_Terabytes_RendersWithTbSuffix() {
        Assert.Equal("3 TB", FileSizeFormatter.Format(3 * Tb));
    }

    [Fact]
    public void Format_Negative_ClampsToZeroBytes() {
        Assert.Equal("0 B", FileSizeFormatter.Format(-100));
    }
}
