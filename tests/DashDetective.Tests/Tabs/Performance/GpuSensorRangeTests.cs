using DashDetective.Services.SystemMetrics;
using DashDetective.Tabs.Performance;
using Xunit;

namespace DashDetective.Tests.Tabs.Performance;

/// <summary>
/// Covers the sensor plausibility windows, which the NVIDIA, AMD and Linux GPU readers now share
/// instead of each carrying their own constants. The bounds are the whole point: a vendor SDK reports
/// <c>0</c> for a sensor a card does not have, so an unfiltered reading draws a board at absolute zero.
/// </summary>
public class GpuSensorRangeTests {
    [Theory]
    [InlineData(1)]      // the floor itself
    [InlineData(43)]
    [InlineData(95)]
    [InlineData(150)]    // the ceiling itself
    public void Celsius_PlausibleReading_IsAccepted(double celsius) =>
        Assert.Equal(celsius, GpuSensorRange.Celsius(celsius));

    [Theory]
    [InlineData(0)]      // "not reported", the case this exists for
    [InlineData(-40)]
    [InlineData(151)]
    [InlineData(5000)]
    public void Celsius_ImplausibleReading_ReadsAsNoReading(double celsius) =>
        Assert.Null(GpuSensorRange.Celsius(celsius));

    [Fact]
    public void Celsius_Null_StaysNull() => Assert.Null(GpuSensorRange.Celsius(null));

    [Theory]
    [InlineData(0.1)]    // an integrated adapter idling below a watt
    [InlineData(0.5)]
    [InlineData(230)]
    [InlineData(2000)]
    public void Watts_PlausibleReading_IsAccepted(double watts) =>
        Assert.Equal(watts, GpuSensorRange.Watts(watts));

    /// <summary>The floor is 0.1 W rather than 1 W so a sub-watt integrated card is not blanked. AMD's
    /// reader used to floor at 1 W; its readings are whole watts, so no real value falls in the gap and
    /// unifying the two changed nothing it reports.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(0.05)]
    [InlineData(-5)]
    [InlineData(230_000)]   // milliwatts mistaken for watts
    public void Watts_ImplausibleReading_ReadsAsNoReading(double watts) =>
        Assert.Null(GpuSensorRange.Watts(watts));

    /// <summary>A drive tolerates far less heat than a board, so the two windows are deliberately
    /// different rather than one shared constant: 130 °C is a bad reading from a disk and a hot but real
    /// one from a GPU.</summary>
    [Fact]
    public void DiskCeiling_IsLowerThanTheGpuCeiling() {
        Assert.Null(DiskTemperatureRange.Celsius(130));
        Assert.Equal(130, GpuSensorRange.Celsius(130));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(38)]
    [InlineData(125)]
    public void DiskTemperature_PlausibleReading_IsAccepted(double celsius) =>
        Assert.Equal(celsius, DiskTemperatureRange.Celsius(celsius));

    [Theory]
    [InlineData(0)]
    [InlineData(-273)]   // an absent NVMe reading, which arrives in Kelvin
    [InlineData(126)]
    public void DiskTemperature_ImplausibleReading_ReadsAsNoReading(double celsius) =>
        Assert.Null(DiskTemperatureRange.Celsius(celsius));
}
