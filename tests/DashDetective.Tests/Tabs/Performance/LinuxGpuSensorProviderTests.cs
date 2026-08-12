using DashDetective.Services.SystemMetrics;
using DashDetective.Tabs.Performance;
using DashDetective.Tests.Fakes;
using System.Threading.Tasks;
using Xunit;

namespace DashDetective.Tests.Tabs.Performance;

/// <summary>Covers <see cref="LinuxGpuSensorProvider"/>: the three sysfs unit scales, the plausibility
/// windows that catch a wrong one, and that a card with no hwmon reports nothing rather than zero.</summary>
public class LinuxGpuSensorProviderTests {
    private static readonly GpuPciId AmdPci = new(0x1002, 0x73DF, 0x0E3B1002, 0xC7);

    /// <summary>millidegrees ÷ 1000 and microwatts ÷ 1e6 — 52000 is 52 °C, 45000000 is 45 W. Reading either
    /// at the wrong scale still produces a number that looks like a temperature or a wattage, which is why
    /// both are pinned to exact values.</summary>
    [Fact]
    public void Read_ConvertsBothSysfsUnitScales() {
        var provider = new LinuxGpuSensorProvider(new FakeProcFileSystem().WithAmdgpuCard());

        var sample = provider.Read("0000:03:00.0", AmdPci);

        Assert.Equal(52, sample.TemperatureCelsius);
        Assert.Equal(45, sample.PowerWatts);
    }

    /// <summary>The card is found by its adapter key alone — no PCI matching, unlike the Windows fan-out.
    /// Passing no PCI identity at all must change nothing.</summary>
    [Fact]
    public void Read_IgnoresThePciArgumentEntirely() {
        var provider = new LinuxGpuSensorProvider(new FakeProcFileSystem().WithAmdgpuCard());

        Assert.Equal(provider.Read("0000:03:00.0", AmdPci), provider.Read("0000:03:00.0", null));
    }

    /// <summary>The proprietary NVIDIA driver registers no hwmon, so there is nothing to read — both tiles
    /// stay "—" rather than showing a card at 0 °C.</summary>
    [Fact]
    public void Read_CardWithNoHwmon_ReportsNothing() {
        var provider = new LinuxGpuSensorProvider(new FakeProcFileSystem().WithNvidiaCard());

        Assert.Equal(GpuSensorSample.None, provider.Read("0000:01:00.0", null));
    }

    [Fact]
    public void Read_UnknownAdapter_ReportsNothing() {
        var provider = new LinuxGpuSensorProvider(new FakeProcFileSystem().WithAmdgpuCard());

        Assert.Equal(GpuSensorSample.None, provider.Read("0000:99:00.0", null));
    }

    /// <summary>Temperature and power blank independently: a driver that publishes one and not the other
    /// must not lose both.</summary>
    [Fact]
    public void Read_PowerAbsent_StillReportsTemperature() {
        var proc = new FakeProcFileSystem()
            .WithFile("/sys/class/drm/card0/device/vendor", "0x1002\n")
            .WithLink("/sys/class/drm/card0/device", "/sys/devices/pci0000:00/0000:03:00.0")
            .WithFile("/sys/class/drm/card0/device/hwmon/hwmon4/name", "amdgpu\n")
            .WithFile("/sys/class/drm/card0/device/hwmon/hwmon4/temp1_input", "61000\n");

        var sample = new LinuxGpuSensorProvider(proc).Read("0000:03:00.0", null);

        Assert.Equal(61, sample.TemperatureCelsius);
        Assert.Null(sample.PowerWatts);
    }

    /// <summary>Some drivers publish only the instantaneous figure.</summary>
    [Fact]
    public void Read_FallsBackToPowerInputWhenThereIsNoAverage() {
        var proc = new FakeProcFileSystem()
            .WithFile("/sys/class/drm/card0/device/vendor", "0x1002\n")
            .WithLink("/sys/class/drm/card0/device", "/sys/devices/pci0000:00/0000:03:00.0")
            .WithFile("/sys/class/drm/card0/device/hwmon/hwmon4/name", "amdgpu\n")
            .WithFile("/sys/class/drm/card0/device/hwmon/hwmon4/power1_input", "18500000\n");

        Assert.Equal(18.5, new LinuxGpuSensorProvider(proc).Read("0000:03:00.0", null).PowerWatts);
    }

    [Theory]
    [InlineData("52000\n", 1000.0, 52.0)]
    [InlineData("45000000\n", 1_000_000.0, 45.0)]
    [InlineData("0\n", 1000.0, 0.0)]
    public void ParseScaled_DividesByTheSubUnit(string text, double scale, double expected) {
        Assert.Equal(expected, LinuxGpuSensorProvider.ParseScaled(text, scale));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("N/A\n")]
    public void ParseScaled_UnreadableIsNull(string? text) {
        Assert.Null(LinuxGpuSensorProvider.ParseScaled(text, 1000.0));
    }

    /// <summary>A driver reporting 0 for a sensor it does not have would otherwise show the card at
    /// absolute zero.</summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(-40.0)]
    [InlineData(200.0)]
    [InlineData(null)]
    public void PlausibleCelsius_RejectsImpossibleReadings(double? celsius) {
        Assert.Null(LinuxGpuSensorProvider.PlausibleCelsius(celsius));
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(52.0)]
    [InlineData(150.0)]
    public void PlausibleCelsius_AcceptsTheRealRange(double celsius) {
        Assert.Equal(celsius, LinuxGpuSensorProvider.PlausibleCelsius(celsius));
    }

    /// <summary>The window is also the wrong-scale check: microwatts read as watts land far above the
    /// ceiling, and watts read as microwatts far below the floor.</summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.05)]
    [InlineData(45_000_000.0)]
    [InlineData(null)]
    public void PlausibleWatts_RejectsImpossibleReadings(double? watts) {
        Assert.Null(LinuxGpuSensorProvider.PlausibleWatts(watts));
    }

    [Fact]
    public void Read_NoDrmTree_ReportsNothing() {
        var provider = new LinuxGpuSensorProvider(new FakeProcFileSystem());

        Assert.Equal(GpuSensorSample.None, provider.Read("0000:03:00.0", null));
    }

    /// <summary>The adapter key the Performance tab passes in comes from the enumeration, so the two have to
    /// agree here just as the utilisation sampler does.</summary>
    [Fact]
    public async Task Read_AcceptsTheKeyTheAdapterEnumerationEmits() {
        var proc = new FakeProcFileSystem().WithAmdgpuCard();
        var adapters = await new LinuxGpuAdapterProvider(proc).GetAsync();
        var provider = new LinuxGpuSensorProvider(proc);

        Assert.Equal(52, provider.Read(Assert.Single(adapters).LuidToken, null).TemperatureCelsius);
    }

    /// <summary>The real constructor reads the live filesystem; on a box with no <c>/sys</c> that must be
    /// no readings rather than a throw.</summary>
    [Fact]
    public void RealFileSystem_SoftFailsToNothing() {
        if (System.OperatingSystem.IsLinux())
            return;

        using var provider = new LinuxGpuSensorProvider();

        Assert.Equal(GpuSensorSample.None, provider.Read("0000:03:00.0", null));
    }
}
