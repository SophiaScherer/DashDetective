using DashDetective.Services.SystemMetrics;
using Xunit;

namespace DashDetective.Tests.Services.SystemMetrics;

/// <summary>Covers <see cref="IGpuDetailsProvider.ForCurrentPlatform"/>: one cached reader for the whole
/// process, so a refresh or a second page never re-probes Direct3D for an adapter already read.</summary>
public class GpuDetailsProviderSeamTests {
    [Fact]
    public void ForCurrentPlatform_IsOneCachedReaderPerProcess() {
        var first = IGpuDetailsProvider.ForCurrentPlatform();

        Assert.IsType<GpuDetailsCache>(first);
        Assert.Same(first, IGpuDetailsProvider.ForCurrentPlatform());
    }
}
