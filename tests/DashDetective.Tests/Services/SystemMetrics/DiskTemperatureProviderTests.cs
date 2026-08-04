using DashDetective.Services.SystemMetrics;
using System;
using Xunit;

namespace DashDetective.Tests.Services.SystemMetrics;

/// <summary>Covers <see cref="WindowsDiskTemperatureProvider.KelvinToCelsius"/>: the Kelvin→°C conversion and the
/// plausibility clamp that rejects "not reported" (0 K) and out-of-range readings. The IOCTL itself is not
/// unit-tested (it needs a real NVMe drive), mirroring how the raw samplers are left to integration.
///
/// The conversion is pure and would run anywhere; it is gated only because it inherits its type's
/// <c>[SupportedOSPlatform("windows")]</c>, which the NVMe IOCTL genuinely needs. M13 owns Linux disk
/// temperature and is where the conversion would earn a platform-neutral home.</summary>
public class DiskTemperatureProviderTests {
    [Theory]
    [InlineData(324, 51)]   // typical NVMe reading
    [InlineData(300, 27)]
    public void KelvinToCelsius_PlausibleReading_Converts(ushort kelvin, double expectedCelsius) {
        if (!OperatingSystem.IsWindows())
            return;

        Assert.Equal(expectedCelsius, WindowsDiskTemperatureProvider.KelvinToCelsius(kelvin));
    }

    [Theory]
    [InlineData(0)]     // "not reported" → 0 K
    [InlineData(273)]   // 0 °C, below the plausible floor
    [InlineData(500)]   // 227 °C, absurdly high
    public void KelvinToCelsius_ImplausibleReading_ReturnsNull(ushort kelvin) {
        if (!OperatingSystem.IsWindows())
            return;

        Assert.Null(WindowsDiskTemperatureProvider.KelvinToCelsius(kelvin));
    }
}
