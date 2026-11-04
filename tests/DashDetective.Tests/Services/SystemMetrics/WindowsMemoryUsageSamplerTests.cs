using DashDetective.Services.SystemMetrics;
using System;
using Xunit;

namespace DashDetective.Tests.Services.SystemMetrics;

/// <summary>Pins that a failed native read on a working <see cref="WindowsMemoryUsageSampler"/> throws, so
/// <c>MetricChannel</c> can show the placeholder, while the latched-inert zero contract is unchanged.</summary>
public class WindowsMemoryUsageSamplerTests {
    [Fact]
    public void EnsureRead_Failure_Throws() {
        Assert.Throws<InvalidOperationException>(() => WindowsMemoryUsageSampler.EnsureRead(false));
    }

    [Fact]
    public void EnsureRead_Success_DoesNotThrow() {
        WindowsMemoryUsageSampler.EnsureRead(true);
    }

    [Fact]
    public void Sample_LatchedInert_StillReturnsZeroReading() {
        Assert.Equal(0, new WindowsMemoryUsageSampler(SamplerInit.Inert).Sample().LoadPercent);
    }
}
