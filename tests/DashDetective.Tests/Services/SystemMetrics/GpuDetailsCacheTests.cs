using DashDetective.Services.SystemMetrics;
using DashDetective.Tests.Fakes;
using System;
using System.Threading.Tasks;
using Xunit;

namespace DashDetective.Tests.Services.SystemMetrics;

/// <summary>Covers <see cref="GpuDetailsCache"/>: each adapter is read once per process, only unseen
/// adapters reach the reader, one it had nothing for is asked again, and a reader that throws caches
/// nothing.</summary>
public class GpuDetailsCacheTests {
    private static readonly GpuDetails Rtx = new("32.0.16.1062", "12 (FL 12.2)", "PCI bus 1, device 0, function 0");
    private static readonly GpuDetails Radeon = new("32.0.12019.1028", "12 (FL 12.1)", "PCI bus 15, device 0, function 0");

    [Fact]
    public async Task GetAsync_SecondRequest_IsServedWithoutReadingAgain() {
        var reader = new FakeGpuDetailsProvider().With("a", Rtx);
        var cache = new GpuDetailsCache(reader);

        await cache.GetAsync(["a"]);
        var again = await cache.GetAsync(["a"]);

        Assert.Equal(Rtx, again["a"]);
        Assert.Single(reader.Requests);
    }

    [Fact]
    public async Task GetAsync_NewAdapter_ReadsOnlyThatOne() {
        var reader = new FakeGpuDetailsProvider().With("a", Rtx).With("b", Radeon);
        var cache = new GpuDetailsCache(reader);

        await cache.GetAsync(["a"]);
        var both = await cache.GetAsync(["a", "b"]);

        Assert.Equal(["b"], reader.Requests[1]);
        Assert.Equal(Radeon, both["b"]);
        Assert.Equal(Rtx, both["a"]);
    }

    [Fact]
    public async Task GetAsync_AdapterTheReaderDoesNotKnow_IsAbsentAndAskedAgain() {
        var reader = new FakeGpuDetailsProvider();
        var cache = new GpuDetailsCache(reader);

        Assert.Empty(await cache.GetAsync(["a"]));
        await cache.GetAsync(["a"]);

        Assert.Equal(2, reader.Requests.Count);
    }

    /// <summary>The reader is contracted not to throw; one that does reaches the caller and leaves nothing
    /// cached, so a later request reads afresh.</summary>
    [Fact]
    public async Task GetAsync_ReaderThrows_PropagatesAndCachesNothing() {
        var reader = new FakeGpuDetailsProvider().Throwing();
        var cache = new GpuDetailsCache(reader);

        await Assert.ThrowsAsync<InvalidOperationException>(() => cache.GetAsync(["a"]));
        await Assert.ThrowsAsync<InvalidOperationException>(() => cache.GetAsync(["a"]));

        Assert.Equal(2, reader.Requests.Count);
    }
}
