using DashDetective.Services.SystemMetrics;
using Xunit;

namespace DashDetective.Tests.Services.SystemMetrics;

/// <summary>Covers <see cref="SystemPerformanceProvider.ToBytes"/>: scaling the reported page count by the
/// system page size, and rejecting the "not reported" zeros and an overflowing product. The
/// <c>GetPerformanceInfo</c> call itself is not unit-tested (it reads live machine state), mirroring how the
/// raw samplers are left to integration.</summary>
public class SystemPerformanceProviderTests {
    [Theory]
    [InlineData(1_500_000UL, 4096UL, 6_144_000_000UL)]  // ~6 GB cached on the usual 4 KiB page
    [InlineData(1UL, 4096UL, 4096UL)]
    [InlineData(2048UL, 8192UL, 16_777_216UL)]          // a large-page system still scales correctly
    public void ToBytes_PlausibleReading_ScalesByThePageSize(ulong pages, ulong pageSize, ulong expected) {
        Assert.Equal(expected, SystemPerformanceProvider.ToBytes(pages, pageSize));
    }

    [Theory]
    [InlineData(0UL, 4096UL)]   // "not reported" → no cache pages
    [InlineData(1_500_000UL, 0UL)]
    [InlineData(0UL, 0UL)]
    public void ToBytes_ZeroInput_ReturnsNull(ulong pages, ulong pageSize) {
        Assert.Null(SystemPerformanceProvider.ToBytes(pages, pageSize));
    }

    /// <summary>A garbage reading must not wrap around into a small, plausible-looking byte count.</summary>
    [Fact]
    public void ToBytes_OverflowingProduct_ReturnsNull() {
        Assert.Null(SystemPerformanceProvider.ToBytes(ulong.MaxValue, 4096));
    }

    /// <summary>The largest product that still fits is returned rather than rejected by the guard.</summary>
    [Fact]
    public void ToBytes_ProductAtTheLimit_StillReturnsAValue() {
        Assert.Equal(ulong.MaxValue - 1, SystemPerformanceProvider.ToBytes((ulong.MaxValue - 1) / 2, 2));
    }
}
