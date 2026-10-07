using DashDetective.Shared;
using Xunit;

namespace DashDetective.Tests.Shared;

/// <summary>
/// Pins <see cref="ByteRateFormatter"/>'s contract: binary KB/s, MB/s and GB/s by magnitude to match
/// Task Manager's disk pane, a low rate never collapsing to "0.0 MB/s", an exact zero reading "0 KB/s",
/// and the unit chosen from the value as displayed so "1024" of a unit is never shown.
/// </summary>
public class ByteRateFormatterTests {
    private const double KiB = 1024;
    private const double MiB = 1024 * KiB;
    private const double GiB = 1024 * MiB;

    [Fact]
    public void Format_Zero_ReadsZeroKilobytes() => Assert.Equal("0 KB/s", ByteRateFormatter.Format(0));

    [Theory]
    [InlineData(-5)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Format_NegativeOrNonFinite_ReadsAsZero(double bytesPerSec) =>
        Assert.Equal("0 KB/s", ByteRateFormatter.Format(bytesPerSec));

    [Fact]
    public void Format_TrickleThatWouldReadZeroPointZero_ReadsPlainZero() =>
        Assert.Equal("0 KB/s", ByteRateFormatter.Format(40));

    [Fact]
    public void Format_BelowOneKilobyte_ShowsOneDecimalOfAKilobyte() =>
        Assert.Equal("0.5 KB/s", ByteRateFormatter.Format(512));

    [Fact]
    public void Format_LowRate_StaysInKilobytesRatherThanZeroMegabytes() {
        var text = ByteRateFormatter.Format(300 * KiB);

        Assert.Equal("300 KB/s", text);
        Assert.DoesNotContain("MB/s", text);
    }

    [Theory]
    [InlineData(8 * KiB, "8.0 KB/s")]
    [InlineData(123 * KiB, "123 KB/s")]
    public void Format_Kilobytes_UsesOneDecimalBelowTenAndWholeAbove(double bytesPerSec, string expected) =>
        Assert.Equal(expected, ByteRateFormatter.Format(bytesPerSec));

    [Fact]
    public void Format_JustUnderThePromotionPoint_StaysInKilobytes() =>
        Assert.Equal("1023 KB/s", ByteRateFormatter.Format(1023.4 * KiB));

    [Fact]
    public void Format_ThatWouldRoundTo1024Kilobytes_PromotesToMegabytes() =>
        Assert.Equal("1.0 MB/s", ByteRateFormatter.Format(1023.6 * KiB));

    [Theory]
    [InlineData(1 * MiB, "1.0 MB/s")]
    [InlineData(1.5 * MiB, "1.5 MB/s")]
    [InlineData(48 * MiB, "48 MB/s")]
    public void Format_Megabytes_UsesOneDecimalBelowTenAndWholeAbove(double bytesPerSec, string expected) =>
        Assert.Equal(expected, ByteRateFormatter.Format(bytesPerSec));

    [Fact]
    public void Format_JustUnderOneGigabyte_StaysInMegabytes() =>
        Assert.Equal("1023 MB/s", ByteRateFormatter.Format(1023.4 * MiB));

    [Fact]
    public void Format_ThatWouldRoundTo1024Megabytes_PromotesToGigabytes() =>
        Assert.Equal("1.0 GB/s", ByteRateFormatter.Format(1023.6 * MiB));

    [Theory]
    [InlineData(1 * GiB, "1.0 GB/s")]
    [InlineData(2.5 * GiB, "2.5 GB/s")]
    public void Format_Gigabytes_UsesOneDecimalBelowTen(double bytesPerSec, string expected) =>
        Assert.Equal(expected, ByteRateFormatter.Format(bytesPerSec));

    [Fact]
    public void Format_BeyondAThousandGigabytes_StaysInGigabytes() =>
        Assert.Equal("2048 GB/s", ByteRateFormatter.Format(2048 * GiB));
}
