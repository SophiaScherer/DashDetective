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
