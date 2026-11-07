using DashDetective.Services.SystemMetrics;
using Xunit;

namespace DashDetective.Tests.Services.SystemMetrics;

/// <summary>Pins the dispose contract: disposing twice is safe, and Sample after disposal is empty rather than a collect on a closed query.</summary>
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
