using DashDetective.Tabs.Performance;
using System.Globalization;
using Xunit;

namespace DashDetective.Tests.Tabs.Performance;

/// <summary>Covers <see cref="GpuSensorFormatter"/>: rendering a vendor SDK's temperature and power readings
/// as whole degrees and watts, and falling back to the neutral placeholder when a reading is missing — which
/// is the normal state for a vendor with no reader.</summary>
public class GpuSensorFormatterTests {
    [Theory]
    [InlineData(41, "41 °C")]        // the RTX 3060 at idle, cross-checked against nvidia-smi
    [InlineData(72.4, "72 °C")]
    [InlineData(72.5, "73 °C")]      // standard numeric formatting rounds halves away from zero
    [InlineData(0, "0 °C")]          // a real zero reading is not the placeholder
    [InlineData(-5, "-5 °C")]        // sub-zero is implausible but honest if reported
    public void FormatTemperature_Reading_RendersWholeDegrees(double celsius, string expected) {
        Assert.Equal(expected, GpuSensorFormatter.FormatTemperature(celsius));
    }

    [Theory]
    [InlineData(16.42, "16 W")]      // the RTX 3060 at idle, cross-checked against nvidia-smi
    [InlineData(170.0, "170 W")]
    [InlineData(0, "0 W")]
    public void FormatPower_Reading_RendersWholeWatts(double watts, string expected) {
        Assert.Equal(expected, GpuSensorFormatter.FormatPower(watts));
    }

    [Fact]
    public void FormatTemperature_NoReading_ReturnsPlaceholder() {
        Assert.Equal("—", GpuSensorFormatter.FormatTemperature(null));
    }

    [Fact]
    public void FormatPower_NoReading_ReturnsPlaceholder() {
        Assert.Equal("—", GpuSensorFormatter.FormatPower(null));
    }

    [Fact]
    public void Format_NaNReading_ReturnsPlaceholder() {
        Assert.Equal("—", GpuSensorFormatter.FormatTemperature(double.NaN));
        Assert.Equal("—", GpuSensorFormatter.FormatPower(double.NaN));
    }

    /// <summary>The degree symbol and digits are culture-independent, matching the rest of the app's
    /// InvariantCulture formatting.</summary>
    [Fact]
    public void Format_UnderADifferentCulture_IsUnchanged() {
        var previous = CultureInfo.CurrentCulture;
        try {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            Assert.Equal("41 °C", GpuSensorFormatter.FormatTemperature(41));
            Assert.Equal("170 W", GpuSensorFormatter.FormatPower(170));
        } finally {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
