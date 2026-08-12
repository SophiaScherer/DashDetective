using DashDetective.Services.SystemMetrics;
using DashDetective.Tabs.Performance;
using System;
using Xunit;

namespace DashDetective.Tests.Tabs.Performance;

/// <summary>Pins the <see cref="IGpuSensorProvider"/> seam: which provider the platform resolves to, and
/// that the no-sensors arm reports nothing rather than throwing — it runs on every tick of the Performance
/// tab's throughput timer, so a throw there would be relentless.</summary>
public class GpuSensorProviderSeamTests {
    private static readonly GpuPciId Nvidia = new(0x10DE, 0x2504, 0x397D1462, 0xA1);

    [Fact]
    public void ForCurrentPlatform_ResolvesTheProviderForThisHost() {
        using var provider = IGpuSensorProvider.ForCurrentPlatform();

        if (OperatingSystem.IsWindows()) {
            Assert.IsType<WindowsGpuSensorProvider>(provider);
        } else if (OperatingSystem.IsLinux()) {
            // Spelled out rather than folded into the else so the `OperatingSystem.IsLinux` grep finds this
            // file when the sysfs sensor reader lands — a Windows run never executes this branch.
            Assert.IsType<UnsupportedGpuSensorProvider>(provider);
        } else {
            Assert.IsType<UnsupportedGpuSensorProvider>(provider);
        }
    }

    [Fact]
    public void Unsupported_ReportsNothingForAnyAdapter() {
        var provider = new UnsupportedGpuSensorProvider();

        Assert.Equal(GpuSensorSample.None, provider.Read("0000:01:00.0", Nvidia));
        Assert.Equal(GpuSensorSample.None, provider.Read("luid_0x00000000_0x0000e54b", null));
        provider.Dispose();
        provider.Dispose();
    }

    /// <summary>Whatever this host resolves to, reading it must not throw: the Performance tab calls it once
    /// per GPU per tick, and asserts no values so it holds on a machine with sensors and one without.</summary>
    [Fact]
    public void ForCurrentPlatform_ReadingNeverThrows() {
        using var provider = IGpuSensorProvider.ForCurrentPlatform();

        _ = provider.Read("0000:01:00.0", Nvidia);
        _ = provider.Read("nonsense", null);
    }
}
