using DashDetective.Services.SystemMetrics;
using DashDetective.Tabs.Dashboard;
using DashDetective.Tests.Fakes;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace DashDetective.Tests.Tabs.Dashboard;

/// <summary>
/// Pins <see cref="DashboardViewModel.InitialLoad"/> as the thing tests await in place of a fixed sleep: it must
/// not finish before the constructor's device loads have built their cards, or awaiting it races them again.
/// </summary>
public class DashboardInitialLoadTests {
    private const string Amd = "0000:03:00.0";

    [Fact]
    public async Task InitialLoad_CompletesOnlyAfterTheGpuCardsAreBuilt() {
        var adapters = new TaskCompletionSource<IReadOnlyList<GpuAdapter>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var viewModel = new DashboardViewModel(
            TestMetrics.Idle(),
            StubHardwareProviders.Compose(gpuAdapters: () => adapters.Task),
            () => new FakeGpuUsageSampler().Reporting(Amd, 37));

        Assert.False(viewModel.InitialLoad.IsCompleted);
        Assert.DoesNotContain(viewModel.Cards, c => c.Category == DeviceCategory.Gpu);

        adapters.SetResult([new GpuAdapter(Amd, "AMD Radeon RX 6700 XT", false, 0)]);
        await viewModel.InitialLoad;

        Assert.Single(viewModel.Cards, c => c.Category == DeviceCategory.Gpu);
    }

    [Fact]
    public async Task InitialLoad_FailingProvider_StillCompletesWithoutFaulting() {
        var viewModel = new DashboardViewModel(
            TestMetrics.Idle(),
            StubHardwareProviders.Compose(
                gpuAdapters: () => Task.FromException<IReadOnlyList<GpuAdapter>>(new InvalidOperationException())),
            () => new FakeGpuUsageSampler());

        await viewModel.InitialLoad;

        Assert.True(viewModel.InitialLoad.IsCompletedSuccessfully);
    }
}
