using DashDetective.Services.SystemMetrics;
using Xunit;

namespace DashDetective.Tests.Services.SystemMetrics;

/// <summary>Pins the dispose contract of <see cref="WindowsPhysicalDiskThroughputSampler"/>: safe to dispose
/// more than once, and empty (not a collect on a closed query) after disposal.</summary>
public class WindowsPhysicalDiskThroughputSamplerTests {
    [Fact]
    public void Dispose_Twice_IsSafeAndSampleAfterwardsIsEmpty() {
        var sampler = new WindowsPhysicalDiskThroughputSampler(SamplerInit.Inert);

        sampler.Dispose();
        sampler.Dispose();

        Assert.True(sampler.IsDisposed);
        Assert.Empty(sampler.Sample());
    }
}
