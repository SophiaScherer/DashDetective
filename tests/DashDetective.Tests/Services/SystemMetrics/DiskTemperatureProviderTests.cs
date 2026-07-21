using DashDetective.Services.SystemMetrics;
using Xunit;

namespace DashDetective.Tests.Services.SystemMetrics;

/// <summary>Covers <see cref="DiskTemperatureProvider.KelvinToCelsius"/>: the Kelvin→°C conversion and the
/// plausibility clamp that rejects "not reported" (0 K) and out-of-range readings. The IOCTL itself is not
/// unit-tested (it needs a real NVMe drive), mirroring how the raw samplers are left to integration.</summary>
public class DiskTemperatureProviderTests {
    [Theory]
    [InlineData(324, 51)]   // typical NVMe reading
    [InlineData(300, 27)]
    public void KelvinToCelsius_PlausibleReading_Converts(ushort kelvin, double expectedCelsius) {
        Assert.Equal(expectedCelsius, DiskTemperatureProvider.KelvinToCelsius(kelvin));
    }

    [Theory]
    [InlineData(0)]     // "not reported" → 0 K
    [InlineData(273)]   // 0 °C, below the plausible floor
    [InlineData(500)]   // 227 °C, absurdly high
    public void KelvinToCelsius_ImplausibleReading_ReturnsNull(ushort kelvin) {
        Assert.Null(DiskTemperatureProvider.KelvinToCelsius(kelvin));
    }
}
