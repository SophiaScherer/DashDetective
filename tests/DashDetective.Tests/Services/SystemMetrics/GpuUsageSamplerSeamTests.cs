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
            // Spelled out rather than folded into the else so the `OperatingSystem.IsLinux` grep finds
            // this file when the Linux sampler lands — a Windows run never executes this branch.
            Assert.IsType<UnsupportedGpuUsageSampler>(sampler);
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

    /// <summary>The inventory intersects this sampler's keys with the adapter enumeration's, so an empty
    /// map has to mean "no GPU cards" rather than throwing or blanking the page.</summary>
    [Fact]
    public void Unsupported_YieldsNoActiveAdapterKeys() {
        if (OperatingSystem.IsWindows())
            return;

        using var sampler = IGpuUsageSampler.ForCurrentPlatform();

        Assert.Empty(sampler.SampleAdapters().Keys);
    }
}
