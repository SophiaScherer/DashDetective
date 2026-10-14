using DashDetective.Services.Network;
using DashDetective.Services.SystemMetrics;
using DashDetective.Tabs.Performance;
using DashDetective.Tests.Fakes;
using System;
using System.Collections.Generic;
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

    /// <summary>The tile says which quantity it shows ("VRAM in use") and gives used against total, both
    /// straight after the load (which seeds the readouts once) and after a tick.</summary>
    [Fact]
    public async Task VramTile_ShowsUsedOverTotal() {
        var total = 12UL << 30;
        var (viewModel, _) = await LoadedAsync(
            () => new FakeGpuUsageSampler().ReportingMemory(Amd, 37, 1185382400),
            new GpuAdapter(Amd, "AMD amdgpu (1002:73df)", false, total));
        var row = viewModel.Resources.Single(r => r.Name.StartsWith("GPU", StringComparison.Ordinal));
        var tile = Assert.Single(row.Stats, t => t.Label == "VRAM in use");

        Assert.DoesNotContain(row.Stats, t => t.Label == "VRAM");
        Assert.Equal("1.1 / 12 GB", tile.Value);

        viewModel.UpdateGpuAdapters();
        Assert.Equal("1.1 / 12 GB", tile.Value);
    }

    [Fact]
    public async Task VramTile_AdapterReportingNoUsage_KeepsTheTotalWithADash() {
        var (viewModel, _) = await LoadedAsync(
            () => new FakeGpuUsageSampler().Reporting(Amd, 37),
            new GpuAdapter(Amd, "AMD amdgpu (1002:73df)", false, 12UL << 30));

        viewModel.UpdateGpuAdapters();

        var row = viewModel.Resources.Single(r => r.Name.StartsWith("GPU", StringComparison.Ordinal));
        Assert.Contains(row.Stats, t => t.Label == "VRAM in use" && t.Value == "— / 12 GB");
    }

    /// <summary>An adapter with no utilization figure can still report memory, so the tile updates ahead of
    /// the utilization guard.</summary>
    [Fact]
    public async Task VramTile_AdapterWithNoUtilization_StillShowsUsage() {
        var (viewModel, _) = await LoadedAsync(
            () => new FakeGpuUsageSampler().Silent(Amd, 2UL << 30),
            new GpuAdapter(Amd, "AMD amdgpu (1002:73df)", false, 8UL << 30));

        viewModel.UpdateGpuAdapters();

        var row = viewModel.Resources.Single(r => r.Name.StartsWith("GPU", StringComparison.Ordinal));
        Assert.Contains(row.Stats, t => t.Label == "VRAM in use" && t.Value == "2.0 / 8.0 GB");
    }

    /// <summary>Shared memory gets its own tile, labeled for its pool, read as used over total and placed
    /// straight after the dedicated one — seeded total-only by the load, then filled by a tick.</summary>
    [Fact]
    public async Task SharedTile_KnownTotal_ShowsUsedOverTotalAfterVram() {
        var (viewModel, sampler) = await LoadedAsync(
            () => new FakeGpuUsageSampler().ReportingMemory(Amd, 37, 1185382400, 3UL << 29),
            new GpuAdapter(Amd, "AMD amdgpu (1002:73df)", false, 12UL << 30, SharedSystemMemory: 16UL << 30));
        var row = viewModel.Resources.Single(r => r.Name.StartsWith("GPU", StringComparison.Ordinal));

        Assert.Equal(["3D", "VRAM in use", "Shared memory in use", "Temp", "Power"], row.Stats.Select(t => t.Label));
        var tile = row.Stats[2];
        Assert.Equal("1.5 / 16 GB", tile.Value);

        sampler.ReportingMemory(Amd, 37, 1185382400, 2UL << 30);
        viewModel.UpdateGpuAdapters();
        Assert.Equal("2.0 / 16 GB", tile.Value);
        Assert.Equal("1.1 / 12 GB", row.Stats[1].Value);
    }

    /// <summary>The acceptance criterion: an adapter with no shared pool omits the row rather than showing a
    /// zero or a dash.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData(0UL)]
    public async Task SharedTile_NoSharedTotal_IsOmitted(ulong? shared) {
        var (viewModel, _) = await LoadedAsync(
            () => new FakeGpuUsageSampler().ReportingMemory(Amd, 37, 1185382400, 5),
            new GpuAdapter(Amd, "AMD amdgpu (1002:73df)", false, 12UL << 30, SharedSystemMemory: shared));

        viewModel.UpdateGpuAdapters();

        var row = viewModel.Resources.Single(r => r.Name.StartsWith("GPU", StringComparison.Ordinal));
        Assert.Equal(["3D", "VRAM in use", "Temp", "Power"], row.Stats.Select(t => t.Label));
    }

    /// <summary>A shared total with no usage reading keeps the total beside a dash, never a fake 0.</summary>
    [Fact]
    public async Task SharedTile_AdapterReportingNoSharedUsage_KeepsTheTotalWithADash() {
        var (viewModel, _) = await LoadedAsync(
            () => new FakeGpuUsageSampler().ReportingMemory(Amd, 37, 1185382400),
            new GpuAdapter(Amd, "AMD amdgpu (1002:73df)", false, 12UL << 30, SharedSystemMemory: 16UL << 30));

        viewModel.UpdateGpuAdapters();

        var row = viewModel.Resources.Single(r => r.Name.StartsWith("GPU", StringComparison.Ordinal));
        Assert.Contains(row.Stats, t => t.Label == "Shared memory in use" && t.Value == "— / 16 GB");
    }

    /// <summary>An integrated GPU: a small dedicated carve-out and a large shared pool, both shown, each
    /// against its own total.</summary>
    [Fact]
    public async Task SharedTile_IntegratedGpu_ShowsBothPools() {
        var (viewModel, _) = await LoadedAsync(
            () => new FakeGpuUsageSampler().ReportingMemory(Amd, 4, 200UL << 20, 1UL << 30),
            new GpuAdapter(Amd, "AMD Radeon(TM) Graphics", false, 512UL << 20, SharedSystemMemory: 15UL << 30));

        viewModel.UpdateGpuAdapters();

        var row = viewModel.Resources.Single(r => r.Name.StartsWith("GPU", StringComparison.Ordinal));
        Assert.Contains(row.Stats, t => t.Label == "VRAM in use" && t.Value == "200 / 512 MB");
        Assert.Contains(row.Stats, t => t.Label == "Shared memory in use" && t.Value == "1.0 / 15 GB");
    }

    /// <summary>A toolbar Refresh rebuilds the row; the shared tile comes back and is filled straight away
    /// rather than waiting a tick at its seed.</summary>
    [Fact]
    public async Task SharedTile_AfterRefresh_IsRebuiltAndFilled() {
        var (viewModel, _) = await LoadedAsync(
            () => new FakeGpuUsageSampler().ReportingMemory(Amd, 37, 1185382400, 3UL << 29),
            new GpuAdapter(Amd, "AMD amdgpu (1002:73df)", false, 12UL << 30, SharedSystemMemory: 16UL << 30));

        await viewModel.LoadInventoryAsync();

        var row = viewModel.Resources.Single(r => r.Name.StartsWith("GPU", StringComparison.Ordinal));
        Assert.Contains(row.Stats, t => t.Label == "Shared memory in use" && t.Value == "1.5 / 16 GB");
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

    /// <summary>The dash gets a reason. Without it, a VM's paravirtual GPU — four dashes and an empty
    /// chart — is indistinguishable from the tab being broken, which is exactly how it was reported.</summary>
    [Fact]
    public async Task LoadInventoryAsync_AdapterThatCannotReport_ExplainsTheDash() {
        var (viewModel, _) = await LoadedAsync(
            () => new FakeGpuUsageSampler().Silent(Amd), Adapter(Amd, "VMware vmwgfx (15ad:0405)"));

        viewModel.UpdateGpuAdapters();

        var row = viewModel.Resources.Single(r => r.Name.StartsWith("GPU", StringComparison.Ordinal));
        Assert.True(row.HasNote);
        Assert.Equal("This GPU's driver publishes no utilization figure.", row.Note);
        Assert.Equal(row.Note, row.NoteTip);
    }

    /// <summary>An adapter that reports carries no note, so the caption line stays out of the way — and the
    /// tooltip is null rather than empty, or hovering the card would pop an empty box.</summary>
    [Fact]
    public async Task LoadInventoryAsync_AdapterThatReports_CarriesNoNote() {
        var (viewModel, _) = await LoadedAsync(
            () => new FakeGpuUsageSampler().Reporting(Amd, 37), Adapter(Amd, "AMD amdgpu (1002:73df)"));

        viewModel.UpdateGpuAdapters();

        var row = viewModel.Resources.Single(r => r.Name.StartsWith("GPU", StringComparison.Ordinal));
        Assert.False(row.HasNote);
        Assert.Equal("", row.Note);
        Assert.Null(row.NoteTip);
    }

    /// <summary>The GPU row by name, with the rail showing every device so a second GPU is listed.</summary>
    private static ResourceRow GpuRow(PerformanceViewModel viewModel, string name = "GPU") {
        viewModel.ShowAllDevices = true;
        return viewModel.Resources.Single(r => r.Name == name);
    }

    /// <summary>NVIDIA publishes all three of Task Manager's other engines, so each gets a tile after 3D,
    /// in Task Manager's order, reading its busiest engine (six Copy engines read the busiest, not a sum).</summary>
    [Fact]
    public async Task EngineTiles_PublishedEngines_EachGetATileInTaskManagersOrder() {
        var (viewModel, _) = await LoadedAsync(
            () => new FakeGpuUsageSampler().Reporting(Amd, 40, new Dictionary<string, double> {
                ["3D"] = 40, ["Copy"] = 7, ["Copy_1"] = 3, ["VideoDecode"] = 12, ["VideoEncode"] = 25,
            }),
            Adapter(Amd, "NVIDIA GeForce RTX 3060"));

        var row = GpuRow(viewModel);
        Assert.Equal(["3D", "Copy", "Video Encode", "Video Decode", "VRAM in use", "Temp", "Power"],
                     row.Stats.Select(t => t.Label));
        Assert.Equal(["40 %", "7 %", "25 %", "12 %"], row.Stats.Take(4).Select(t => t.Value));
    }

    /// <summary>The Radeon iGPU names its encoder "Video Codec", which maps to neither label, so it has no
    /// Video Encode tile rather than one at 0 %.</summary>
    [Fact]
    public async Task EngineTiles_EngineTypeNotPublished_HasNoTile() {
        var (viewModel, _) = await LoadedAsync(
            () => new FakeGpuUsageSampler().Reporting(Amd, 20, new Dictionary<string, double> {
                ["3D"] = 5, ["Copy"] = 0, ["Video Decode"] = 0, ["Video Codec 0"] = 20,
            }),
            Adapter(Amd, "AMD Radeon(TM) Graphics"));

        var labels = GpuRow(viewModel).Stats.Select(t => t.Label).ToList();
        Assert.Equal(["3D", "Copy", "Video Decode", "VRAM in use", "Temp", "Power"], labels);
    }

    /// <summary>A platform with no engine map (Linux) gets none of the three tiles.</summary>
    [Fact]
    public async Task EngineTiles_NoEngineMap_NoEngineTiles() {
        var (viewModel, _) = await LoadedAsync(
            () => new FakeGpuUsageSampler().Reporting(Amd, 37), Adapter(Amd, "AMD amdgpu (1002:73df)"));

        viewModel.UpdateGpuAdapters();

        Assert.Equal(["3D", "VRAM in use", "Temp", "Power"], GpuRow(viewModel).Stats.Select(t => t.Label));
    }

    /// <summary>An engine first seen on a later tick gains its tile then; once it exists, a tick that does
    /// not mention it reads 0 %, as its chart does.</summary>
    [Fact]
    public async Task EngineTiles_EngineAppearingLater_GainsItsTile() {
        var (viewModel, sampler) = await LoadedAsync(
            () => new FakeGpuUsageSampler().Reporting(Amd, 40, new Dictionary<string, double> { ["3D"] = 40 }),
            Adapter(Amd, "NVIDIA GeForce RTX 3060"));
        var row = GpuRow(viewModel);
        Assert.DoesNotContain(row.Stats, t => t.Label == "Video Encode");

        sampler.Reporting(Amd, 40, new Dictionary<string, double> { ["3D"] = 40, ["VideoEncode"] = 30 });
        viewModel.UpdateGpuAdapters();
        Assert.Contains(row.Stats, t => t.Label == "Video Encode" && t.Value == "30 %");

        sampler.Reporting(Amd, 40, new Dictionary<string, double> { ["3D"] = 40 });
        viewModel.UpdateGpuAdapters();
        Assert.Contains(row.Stats, t => t.Label == "Video Encode" && t.Value == "0 %");
    }

    /// <summary>A toolbar Refresh rebuilds the row; its engine tiles come back with it rather than waiting
    /// for a tick.</summary>
    [Fact]
    public async Task EngineTiles_AfterRefresh_AreKept() {
        var (viewModel, _) = await LoadedAsync(
            () => new FakeGpuUsageSampler().Reporting(Amd, 40, new Dictionary<string, double> {
                ["3D"] = 40, ["Copy"] = 7,
            }),
            Adapter(Amd, "NVIDIA GeForce RTX 3060"));

        await viewModel.LoadInventoryAsync();

        Assert.Equal(["3D", "Copy", "VRAM in use", "Temp", "Power"], GpuRow(viewModel).Stats.Select(t => t.Label));
    }

    [Fact]
    public async Task Details_PublishedFacts_AreListedUnderTaskManagersLabels() {
        var (viewModel, _) = await LoadedAsync(
            () => new FakeGpuUsageSampler().Reporting(Amd, 37),
            new GpuAdapter(Amd, "NVIDIA GeForce RTX 3060", false, 0,
                           Details: new GpuDetails("32.0.16.1062", "12 (FL 12.2)", "PCI bus 1, device 0, function 0")));

        var row = GpuRow(viewModel);
        Assert.True(row.HasDetails);
        Assert.Equal(["Driver version", "DirectX version", "Physical location"], row.Details.Select(d => d.Key));
        Assert.Equal(["32.0.16.1062", "12 (FL 12.2)", "PCI bus 1, device 0, function 0"],
                     row.Details.Select(d => d.Value));
    }

    /// <summary>The acceptance criterion: a fact the adapter does not expose is omitted, not shown as a dash
    /// or a zero; an adapter with none shows no list.</summary>
    [Fact]
    public async Task Details_UnpublishedFacts_AreOmitted() {
        var (viewModel, _) = await LoadedAsync(
            () => new FakeGpuUsageSampler().Reporting(Amd, 37).Reporting("0000:01:00.0", 5),
            new GpuAdapter(Amd, "AMD amdgpu (1002:73df)", false, 0,
                           Details: new GpuDetails(null, null, "PCI bus 3, device 0, function 0")),
            Adapter("0000:01:00.0", "NVIDIA nvidia (10de:2504)"));

        var amd = GpuRow(viewModel, "GPU 0");
        Assert.Equal(["Physical location"], amd.Details.Select(d => d.Key));
        var bare = GpuRow(viewModel, "GPU 1");
        Assert.False(bare.HasDetails);
        Assert.Empty(bare.Details);
    }

    /// <summary>Two GPUs: each row lists its own adapter's facts and has its own engine tiles.</summary>
    [Fact]
    public async Task Details_MultiGpu_EachRowHasItsOwn() {
        const string Nvidia = "luid_n";
        const string Radeon = "luid_r";
        var (viewModel, _) = await LoadedAsync(
            () => new FakeGpuUsageSampler()
                .Reporting(Nvidia, 30, new Dictionary<string, double> { ["3D"] = 30, ["VideoEncode"] = 9 })
                .Reporting(Radeon, 4, new Dictionary<string, double> { ["3D"] = 4, ["Video Decode"] = 2 }),
            new GpuAdapter(Nvidia, "NVIDIA GeForce RTX 3060", false, 0,
                           Details: new GpuDetails("32.0.16.1062", "12 (FL 12.2)", "PCI bus 1, device 0, function 0")),
            new GpuAdapter(Radeon, "AMD Radeon(TM) Graphics", false, 0,
                           Details: new GpuDetails("32.0.12019.1028", "12 (FL 12.1)", "PCI bus 15, device 0, function 0")));

        var first = GpuRow(viewModel, "GPU 0");
        var second = GpuRow(viewModel, "GPU 1");
        Assert.Equal("32.0.16.1062", first.Details[0].Value);
        Assert.Equal("PCI bus 15, device 0, function 0", second.Details[2].Value);
        Assert.Contains(first.Stats, t => t.Label == "Video Encode" && t.Value == "9 %");
        Assert.DoesNotContain(first.Stats, t => t.Label == "Video Decode");
        Assert.Contains(second.Stats, t => t.Label == "Video Decode" && t.Value == "2 %");
        Assert.DoesNotContain(second.Stats, t => t.Label == "Video Encode");
    }

    [Fact]
    public void FoldEngines_NumberedEnginesOfOneBase_KeepsTheBusiestRatherThanTheSum() {
        var folded = PerformanceViewModel.FoldEngines(new Dictionary<string, double> {
            ["Compute 0"] = 30, ["Compute 1"] = 45, ["3D"] = 20,
        });

        Assert.Equal(45, folded["compute"]);
        Assert.Equal(20, folded["3d"]);
    }

    [Fact]
    public void ThreeDReading_BusierOtherEngine_ReadsTheThreeDEngineNotTheOverall() {
        var sample = new GpuAdapterSample(64, new Dictionary<string, double> { ["3D"] = 15, ["Video Codec 0"] = 64 });

        Assert.Equal(15, PerformanceViewModel.ThreeDReading(sample, 64));
    }

    [Fact]
    public void ThreeDReading_NoEngineMap_FallsBackToTheOverall() {
        var sample = new GpuAdapterSample(40, new Dictionary<string, double>());

        Assert.Equal(40, PerformanceViewModel.ThreeDReading(sample, 40));
    }

    [Fact]
    public void ThreeDReading_EngineMapWithoutThreeD_ReadsZero() {
        var sample = new GpuAdapterSample(30, new Dictionary<string, double> { ["Copy"] = 30 });

        Assert.Equal(0, PerformanceViewModel.ThreeDReading(sample, 30));
    }
}
