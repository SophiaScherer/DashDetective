using DashDetective.Services.Network;
using DashDetective.Services.SystemMetrics;
using DashDetective.Tabs.Dashboard;
using DashDetective.Tests.Fakes;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace DashDetective.Tests.Tabs.Dashboard;

/// <summary>
/// Covers the Dashboard's GPU cards where a reading may be missing — the path Linux reaches for an adapter
/// whose driver publishes no utilisation (the proprietary NVIDIA blob, Intel's i915). Driving it needs the
/// sampler injected, because the page otherwise resolves its own and no dev machine can produce a
/// null-reporting adapter on demand.
/// </summary>
public class DashboardGpuCardTests {
    private const string Amd = "0000:03:00.0";
    private const string Nvidia = "0000:01:00.0";

    /// <summary>
    /// Builds the page and drives one GPU load to completion.
    ///
    /// The constructor fires its own load and forgets it. In the app that continuation resumes on the
    /// dispatcher, so the card collection is only ever rebuilt from the UI thread; in a test there is no
    /// synchronization context, so it lands on the thread pool and would race a load the test starts
    /// itself. Letting the constructor's finish first makes the final state deterministic.
    /// </summary>
    private static async Task<DashboardViewModel> LoadedAsync(
        Func<FakeGpuUsageSampler> sampler, params GpuAdapter[] adapters) =>
        (await LoadedWithSamplerAsync(sampler, adapters)).ViewModel;

    /// <summary>The same load, also handing back the sampler the page kept for itself — the first the factory
    /// minted. The inventory load mints and disposes its own, so the two must not be the same object.</summary>
    private static async Task<(DashboardViewModel ViewModel, FakeGpuUsageSampler PageSampler)>
        LoadedWithSamplerAsync(Func<FakeGpuUsageSampler> sampler, params GpuAdapter[] adapters) {
        var samplers = new MetricSamplers(
            () => 0, () => new MemorySample(0, 0, 0, 0, 0), () => new NetworkSample(0, 0), () => "TestNIC");

        FakeGpuUsageSampler? pageSampler = null;
        var viewModel = new DashboardViewModel(
            new SystemMetricsService(samplers, () => new FakeUiTimer()),
            StubHardwareProviders.With(gpuAdapters: adapters),
            () => {
                var minted = sampler();
                pageSampler ??= minted;
                return minted;
            });

        await Task.Delay(100);
        await viewModel.LoadGpusAsync();
        return (viewModel, pageSampler!);
    }

    private static GpuAdapter Adapter(string key, string name) => new(key, name, false, 0);

    private static DashboardCard GpuCard(DashboardViewModel viewModel) =>
        viewModel.Cards.Single(c => c.Category == DeviceCategory.Gpu);

    /// <summary>The whole point of the nullable reading: the card exists, so the user can see the hardware,
    /// but it shows the placeholder rather than a confident 0%.</summary>
    [Fact]
    public async Task LoadGpusAsync_AdapterThatCannotReport_ShowsThePlaceholder() {
        var viewModel = await LoadedAsync(
            () => new FakeGpuUsageSampler().Silent(Nvidia), Adapter(Nvidia, "NVIDIA nvidia (10de:2504)"));

        var card = GpuCard(viewModel);
        Assert.Equal("—", card.Value);
        // The unit goes with it, so the card reads "—" and not "— %".
        Assert.Equal("", card.Unit);
    }

    [Fact]
    public async Task LoadGpusAsync_AdapterThatReports_ShowsTheValueAndItsUnit() {
        var viewModel = await LoadedAsync(
            () => new FakeGpuUsageSampler().Reporting(Amd, 37), Adapter(Amd, "AMD amdgpu (1002:73df)"));

        var card = GpuCard(viewModel);
        Assert.Equal("37", card.Value);
        Assert.Equal("%", card.Unit);
    }

    /// <summary>A machine with one of each: the reporting card must not be dragged down by its silent
    /// neighbour, and the silent one must not borrow the other's number.</summary>
    [Fact]
    public async Task LoadGpusAsync_MixedAdapters_EachCardShowsItsOwnState() {
        var viewModel = await LoadedAsync(
            () => new FakeGpuUsageSampler().Reporting(Amd, 37).Silent(Nvidia),
            Adapter(Amd, "AMD amdgpu (1002:73df)"),
            Adapter(Nvidia, "NVIDIA nvidia (10de:2504)"));

        var cards = viewModel.Cards.Where(c => c.Category == DeviceCategory.Gpu).ToList();
        Assert.Equal(2, cards.Count);
        Assert.Contains(cards, c => c.Value == "37" && c.Unit == "%");
        Assert.Contains(cards, c => c.Value == "—" && c.Unit == "");
    }

    /// <summary>The busiest-adapter figure the text report quotes must ignore the adapter with no reading
    /// rather than treating it as an idle 0.</summary>
    [Fact]
    public async Task LoadGpusAsync_OverallFigureIgnoresAdaptersWithNoReading() {
        var viewModel = await LoadedAsync(
            () => new FakeGpuUsageSampler().Reporting(Amd, 37).Silent(Nvidia),
            Adapter(Amd, "AMD amdgpu (1002:73df)"),
            Adapter(Nvidia, "NVIDIA nvidia (10de:2504)"));

        Assert.Equal("37", viewModel.GpuValueText);
    }

    /// <summary>On a machine where nothing can report, the report figure stays the placeholder — the false
    /// "GPU 0%" this replaced.</summary>
    [Fact]
    public async Task LoadGpusAsync_NothingReports_LeavesTheOverallFigureBlank() {
        var viewModel = await LoadedAsync(
            () => new FakeGpuUsageSampler().Silent(Nvidia), Adapter(Nvidia, "NVIDIA nvidia (10de:2504)"));

        Assert.Equal("—", viewModel.GpuValueText);
        Assert.Contains("GPU:", viewModel.BuildDiagnosticsReport());
        Assert.DoesNotContain("GPU:          0%", viewModel.BuildDiagnosticsReport());
    }

    /// <summary>The inventory keeps only adapters the sampler also reports, so an enumerated adapter the
    /// sampler has never heard of gets no card at all — the Windows phantom-LUID rule.</summary>
    [Fact]
    public async Task LoadGpusAsync_AdapterTheSamplerDoesNotReport_GetsNoCard() {
        var viewModel = await LoadedAsync(() => new FakeGpuUsageSampler(), Adapter(Amd, "AMD amdgpu (1002:73df)"));

        Assert.DoesNotContain(viewModel.Cards, c => c.Category == DeviceCategory.Gpu);
    }

    /// <summary>
    /// The inventory load must not dispose the sampler this page ticks on. It did once: both pages passed
    /// their own instance as the factory, the load's <c>using</c> closed the Windows PDH query, and every
    /// GPU readout on both tabs went dead for the session while the adapter names still looked right.
    /// </summary>
    [Fact]
    public async Task LoadGpusAsync_LeavesThePagesOwnSamplerUsable() {
        var (_, pageSampler) = await LoadedWithSamplerAsync(
            () => new FakeGpuUsageSampler().Reporting(Amd, 37), Adapter(Amd, "AMD amdgpu (1002:73df)"));

        Assert.False(pageSampler.Disposed);
        Assert.NotEmpty(pageSampler.SampleAdapters());
    }

    /// <summary>The end state that regressed: after the load, a tick still fills the card. Guards the same
    /// bug from the user's side, so it survives a refactor of how the sampler is injected.</summary>
    [Fact]
    public async Task LoadGpusAsync_ThenTicking_StillUpdatesTheCard() {
        var (viewModel, _) = await LoadedWithSamplerAsync(
            () => new FakeGpuUsageSampler().Reporting(Amd, 37), Adapter(Amd, "AMD amdgpu (1002:73df)"));

        var card = GpuCard(viewModel);
        card.Value = "stale";

        viewModel.UpdateGpuAdapters();

        Assert.Equal("37", card.Value);
        Assert.Equal("%", card.Unit);
    }
}
