using DashDetective.Services.SystemMetrics;
using DashDetective.Tabs.Performance;
using System.Globalization;
using Xunit;

namespace DashDetective.Tests.Tabs.Performance;

/// <summary>Covers <see cref="CpuSpeedFormatter.Format"/>: scaling the WMI base clock by the PDH clock
/// ratio into a GHz readout, leaving Turbo boost above the base clock uncapped, and falling back to the
/// neutral placeholder when either input is missing.</summary>
public class CpuSpeedFormatterTests {
    [Theory]
    [InlineData(3200, 100, "3.20 GHz")]     // at base clock
    [InlineData(3200, 50, "1.60 GHz")]      // half the base clock
    [InlineData(3200, 35.4, "1.13 GHz")]    // idle, rounded to two decimals
    [InlineData(2900, 26, "0.75 GHz")]      // sub-GHz still keeps the leading zero
    [InlineData(3600, 12.5, "0.45 GHz")]
    public void Format_AtOrBelowBaseClock_ScalesTheBaseClock(double maxClockMhz, double percent, string expected) {
        Assert.Equal(expected, CpuSpeedFormatter.Format(maxClockMhz, percent));
    }

    [Theory]
    [InlineData(3200, 131.5, "4.21 GHz")]   // Turbo above the base clock
    [InlineData(2900, 172, "4.99 GHz")]
    [InlineData(3200, 200, "6.40 GHz")]
    public void Format_AboveOneHundredPercent_IsNotCapped(double maxClockMhz, double percent, string expected) {
        Assert.Equal(expected, CpuSpeedFormatter.Format(maxClockMhz, percent));
    }

    [Theory]
    [InlineData(0, 100)]                    // CpuStaticInfo.Unknown reports a zero base clock
    [InlineData(-3200, 100)]
    [InlineData(3200, 0)]                   // the sampler returns 0 when its counter is inert
    [InlineData(3200, -1)]
    [InlineData(0, 0)]
    [InlineData(double.NaN, 100)]
    [InlineData(3200, double.NaN)]
    public void Format_MissingBaseClockOrReading_ReturnsPlaceholder(double maxClockMhz, double percent) {
        Assert.Equal("—", CpuSpeedFormatter.Format(maxClockMhz, percent));
    }

    /// <summary>The Linux arm reports an absolute clock, which is used as-is — the base clock is
    /// irrelevant, and is 0 there anyway until the CPU-info milestone lands.</summary>
    [Theory]
    [InlineData(3600, "3.60 GHz")]
    [InlineData(2400.5, "2.40 GHz")]
    [InlineData(800, "0.80 GHz")]
    public void Format_AbsoluteReading_IgnoresTheBaseClock(double mhz, string expected) {
        var sample = new ProcessorClockSample(PercentOfBase: 0, mhz);

        Assert.Equal(expected, CpuSpeedFormatter.Format(maxClockMhz: 0, sample));
        Assert.Equal(expected, CpuSpeedFormatter.Format(maxClockMhz: 3200, sample));
    }

    /// <summary>The Windows arm is unchanged: a ratio still scales the base clock, and the two overloads
    /// must agree exactly or the tile's reading shifts on Windows.</summary>
    [Theory]
    [InlineData(3200, 131.5)]
    [InlineData(3200, 35.4)]
    [InlineData(2900, 26)]
    public void Format_RatioReading_MatchesTheDoubleOverload(double maxClockMhz, double percent) {
        var sample = new ProcessorClockSample(percent, AbsoluteMhz: 0);

        Assert.Equal(CpuSpeedFormatter.Format(maxClockMhz, percent), CpuSpeedFormatter.Format(maxClockMhz, sample));
    }

    /// <summary>A sampler that read nothing yields a default sample — the case a VirtualBox guest with no
    /// <c>cpufreq</c> and no <c>cpu MHz</c> line actually hits.</summary>
    [Fact]
    public void Format_DefaultSample_ReturnsPlaceholder() {
        Assert.Equal("—", CpuSpeedFormatter.Format(maxClockMhz: 3200, default(ProcessorClockSample)));
        Assert.Equal("—", CpuSpeedFormatter.Format(maxClockMhz: 0, default(ProcessorClockSample)));
    }

    /// <summary>The decimal separator is a period regardless of the ambient culture, matching the rest of
    /// the app's InvariantCulture formatting.</summary>
    [Fact]
    public void Format_UnderACommaDecimalCulture_StillUsesAPeriod() {
        var previous = CultureInfo.CurrentCulture;
        try {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            Assert.Equal("4.21 GHz", CpuSpeedFormatter.Format(3200, 131.5));
        } finally {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
