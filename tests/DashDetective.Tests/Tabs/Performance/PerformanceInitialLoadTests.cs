using DashDetective.Services.SystemMetrics;
using DashDetective.Tabs.Performance;
using DashDetective.Tests.Fakes;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace DashDetective.Tests.Tabs.Performance;

/// <summary>Pins <see cref="PerformanceViewModel.InitialLoad"/> as finishing only after the inventory load has built the rail's rows.</summary>
public class PerformanceInitialLoadTests {
    private const string Amd = "0000:03:00.0";

    private static PerformanceViewModel Page(Func<Task<IReadOnlyList<GpuAdapter>>> adapters) =>
        new(TestMetrics.Idle(),
            StubHardwareProviders.Compose(gpuAdapters: adapters),
            () => new FakeGpuUsageSampler().Reporting(Amd, 37),
            gpuDetails: new FakeGpuDetailsProvider());

    [Fact]
    public async Task InitialLoad_CompletesOnlyAfterTheInventoryRowsAreBuilt() {
        var adapters = new TaskCompletionSource<IReadOnlyList<GpuAdapter>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var page = Page(() => adapters.Task);

        Assert.False(page.InitialLoad.IsCompleted);
        Assert.DoesNotContain(page.Resources, r => r.Name.StartsWith("GPU", StringComparison.Ordinal));

        adapters.SetResult([new GpuAdapter(Amd, "AMD Radeon RX 6700 XT", false, 0)]);
        await page.InitialLoad;

        Assert.Single(page.Resources, r => r.Name.StartsWith("GPU", StringComparison.Ordinal));
    }

    [Fact]
    public async Task InitialLoad_FailingProvider_StillCompletesWithoutFaulting() {
        var page = Page(() => Task.FromException<IReadOnlyList<GpuAdapter>>(new InvalidOperationException()));

        await page.InitialLoad;

        Assert.True(page.InitialLoad.IsCompletedSuccessfully);
    }
}
