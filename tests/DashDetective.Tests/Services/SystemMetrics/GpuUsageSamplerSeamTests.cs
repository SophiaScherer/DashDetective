using DashDetective.Services.SystemMetrics;
using System;
using Xunit;

namespace DashDetective.Tests.Services.SystemMetrics;

/// <summary>Pins the <see cref="IGpuUsageSampler"/> seam: which sampler the platform resolves to, and that
/// the no-readings arm honours the same empty contract the old inline guard produced. The fallback is the
/// thing most likely to rot — nothing on a Windows dev box or runner ever executes it.</summary>
public class GpuUsageSamplerSeamTests {
    [Fact]
    public void ForCurrentPlatform_ResolvesTheSamplerForThisHost() {
        using var sampler = IGpuUsageSampler.ForCurrentPlatform();

        if (OperatingSystem.IsWindows()) {
            Assert.IsType<WindowsGpuUsageSampler>(sampler);
        } else if (OperatingSystem.IsLinux()) {
            Assert.IsType<LinuxGpuUsageSampler>(sampler);
        } else {
            Assert.IsType<UnsupportedGpuUsageSampler>(sampler);
        }
    }

    [Fact]
    public void Unsupported_ReportsNoAdaptersAndDisposesQuietly() {
        var sampler = new UnsupportedGpuUsageSampler();

        Assert.Empty(sampler.SampleAdapters());
        Assert.Empty(sampler.SampleAdapters());
        sampler.Dispose();
        sampler.Dispose();
    }

    /// <summary>Whatever this host resolves to, sampling it must not throw — the inventory calls it during
    /// startup composition, where an exception would blank the whole device list. Asserts no values, only
    /// that nothing escapes, so it stays true on a host with a GPU and one without.</summary>
    [Fact]
    public void ForCurrentPlatform_SamplingNeverThrows() {
        using var sampler = IGpuUsageSampler.ForCurrentPlatform();

        _ = sampler.SampleAdapters();
        _ = sampler.SampleAdapters();
    }
}
