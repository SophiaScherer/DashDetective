using DashDetective.Services.Network;
using DashDetective.Services.SystemMetrics;
using DashDetective.Tabs.Performance;
using DashDetective.Tests.Fakes;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace DashDetective.Tests.Tabs.Performance;

/// <summary>
/// Covers the Performance rail's GPU rows across the inventory load — the tab that carries the utilisation
/// %, the sparkline and the "3D" tile. All three went blank once because the load disposed the sampler the
/// page ticks on, so the row identity is the thing these tests deliberately do not check.
/// </summary>
public class PerformanceGpuRowTests {
    private const string Amd = "0000:03:00.0";

    private static GpuAdapter Adapter(string key, string name) => new(key, name, false, 0);

    /// <summary>Builds the page over a staged sampler and drives one inventory load to completion, handing
    /// back the sampler the page kept for itself — the first the factory minted. The inventory load mints
    /// and disposes one of its own, so the two must not be the same object.</summary>
    private static async Task<(PerformanceViewModel ViewModel, FakeGpuUsageSampler PageSampler)>
        LoadedAsync(Func<FakeGpuUsageSampler> sampler, params GpuAdapter[] adapters) {
        var samplers = new MetricSamplers(
            () => 0, () => new MemorySample(0, 0, 0, 0, 0), () => new NetworkSample(0, 0), () => "TestNIC");

        FakeGpuUsageSampler? pageSampler = null;
        var viewModel = new PerformanceViewModel(
            new SystemMetricsService(samplers, () => new FakeUiTimer()),
            StubHardwareProviders.With(gpuAdapters: adapters),
            () => {
                var minted = sampler();
                pageSampler ??= minted;
                return minted;
            });

        // The constructor fires its own load and forgets it; letting that finish first keeps the final row
        // set deterministic, as in the Dashboard's GPU tests.
        await Task.Delay(100);
        await viewModel.LoadInventoryAsync();
        return (viewModel, pageSampler!);
    }

    /// <summary>
    /// The regression itself: both pages used to hand their own sampler to the inventory load, whose
    /// <c>using</c> closed the Windows PDH query. Every later tick then collected on a dead handle and got
    /// nothing, so the rows kept their placeholder while the adapter names still looked right.
    /// </summary>
    [Fact]
    public async Task LoadInventoryAsync_LeavesThePagesOwnSamplerUsable() {
        var (_, pageSampler) = await LoadedAsync(
            () => new FakeGpuUsageSampler().Reporting(Amd, 37), Adapter(Amd, "AMD amdgpu (1002:73df)"));

        Assert.False(pageSampler.Disposed);
        Assert.NotEmpty(pageSampler.SampleAdapters());
    }

    /// <summary>The user-visible end state: after the load, a tick fills the row's %, its unit, its chart
    /// and the "3D" tile. Guards the same bug from the outside, so it survives a refactor of the injection.</summary>
    [Fact]
    public async Task LoadInventoryAsync_ThenTicking_FillsTheRowAndThreeDTile() {
        var (viewModel, _) = await LoadedAsync(
            () => new FakeGpuUsageSampler().Reporting(Amd, 37), Adapter(Amd, "AMD amdgpu (1002:73df)"));

        viewModel.UpdateGpuAdapters();

        var row = viewModel.Resources.Single(r => r.Name.StartsWith("GPU", StringComparison.Ordinal));
        Assert.Equal("37", row.ValueText);
        Assert.Equal("%", row.Unit);
        Assert.NotEmpty(row.Points);
        Assert.Contains(row.Stats, t => t.Label == "3D" && t.Value == "37 %");
    }

    /// <summary>An adapter whose driver publishes no figure keeps the placeholder rather than a confident
    /// zero — the Linux NVIDIA/Intel case, and the thing a blanket "fill it in" fix would break.</summary>
    [Fact]
    public async Task LoadInventoryAsync_AdapterThatCannotReport_KeepsThePlaceholder() {
        var (viewModel, _) = await LoadedAsync(
            () => new FakeGpuUsageSampler().Silent(Amd), Adapter(Amd, "AMD amdgpu (1002:73df)"));

        viewModel.UpdateGpuAdapters();

        var row = viewModel.Resources.Single(r => r.Name.StartsWith("GPU", StringComparison.Ordinal));
        Assert.Equal("—", row.ValueText);
        Assert.Equal("", row.Unit);
    }
}
