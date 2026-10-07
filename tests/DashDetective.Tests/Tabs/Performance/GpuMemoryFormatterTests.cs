using DashDetective.Tabs.Performance;
using Xunit;

namespace DashDetective.Tests.Tabs.Performance;

/// <summary>Pins <see cref="GpuMemoryFormatter"/>'s "used / total" convention, which matches the Memory rail
/// caption: used to one decimal, total whole, GB — and MB for both under 1 GiB. An unknown usage is a dash
/// beside the total, never a fake zero.</summary>
public class GpuMemoryFormatterTests {
    private const ulong Gib = 1UL << 30;
    private const ulong Mib = 1UL << 20;

    [Fact]
    public void Format_Gigabytes_UsedOneDecimalTotalWhole() {
        Assert.Equal("1.1 / 12 GB", GpuMemoryFormatter.Format(1185382400, 12 * Gib));
    }

    [Fact]
    public void Format_TotalUnderOneGib_ReadsMbForBoth() {
        Assert.Equal("0 / 460 MB", GpuMemoryFormatter.Format(0, 460 * Mib));
        Assert.Equal("128 / 512 MB", GpuMemoryFormatter.Format(128 * Mib, 512 * Mib));
    }

    [Fact]
    public void Format_ExactlyOneGib_ReadsGb() {
        Assert.Equal("0.5 / 1 GB", GpuMemoryFormatter.Format(Gib / 2, Gib));
    }

    [Fact]
    public void Format_UnknownUsage_KeepsTheTotalAndNeverShowsZero() {
        Assert.Equal("— / 12 GB", GpuMemoryFormatter.Format(null, 12 * Gib));
        Assert.Equal("— / 460 MB", GpuMemoryFormatter.Format(null, 460 * Mib));
    }

    [Fact]
    public void Format_ZeroUsage_IsARealReading() {
        Assert.Equal("0.0 / 8 GB", GpuMemoryFormatter.Format(0, 8 * Gib));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0UL)]
    public void Format_UnknownTotal_IsABareDash(ulong? total) {
        Assert.Equal("—", GpuMemoryFormatter.Format(1 * Gib, total));
        Assert.Equal("—", GpuMemoryFormatter.Format(null, total));
    }

    [Fact]
    public void Format_Rounds_UsedToTenthsAndTotalToWhole() {
        // 1.26 GiB used rounds up to 1.3; an 11.8 GiB total reads 12.
        Assert.Equal("1.3 / 12 GB", GpuMemoryFormatter.Format((ulong)(1.26 * Gib), (ulong)(11.8 * Gib)));
        Assert.Equal("1.2 / 12 GB", GpuMemoryFormatter.Format((ulong)(1.24 * Gib), (ulong)(11.8 * Gib)));
    }
}
