using DashDetective.Services.Network;
using DashDetective.Services.SystemMetrics;
using DashDetective.Services.Theming;
using DashDetective.Shared;
using DashDetective.Tabs.Performance;
using DashDetective.Tests.Fakes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace DashDetective.Tests.Tabs.Performance;

/// <summary>
/// Pins that a toolbar Refresh, which re-runs the device inventory and rebuilds every disk and GPU row,
/// carries each surviving device's accumulated chart history over rather than restarting it from empty.
/// A rebuilt row seeds one sample of its own, so a surviving trace grows by exactly that one; a device that
/// is new on the reload has only that seed.
/// </summary>
public class PerformanceRefreshHistoryTests {
    private const string Amd = "0000:03:00.0";
    private const string Nvidia = "0000:01:00.0";

    private static readonly PhysicalDiskInfo Boot = new(3, "Boot Drive", "NVMe SSD", 1_000_000_000_000, true);
    private static readonly PhysicalDiskInfo Data = new(4, "Data Drive", "NVMe SSD", 1_000_000_000_000, false);

    private static GpuAdapter Adapter(string key, string name) => new(key, name, false, 0);

    private static IReadOnlyList<VolumeInfo> Volumes() => [
        new(Boot.DeviceId, SystemDrive.Letter, "", "NTFS", 1_000_000_000_000, 500_000_000_000),
    ];

    /// <summary>The page over mutable device lists, so a test can change what a reload finds. Waits out the
    /// load the constructor fires and forgets, so every later count is deterministic.</summary>
    private static async Task<PerformanceViewModel> LoadedAsync(
        List<PhysicalDiskInfo> disks, List<GpuAdapter> gpus, FakeDiskThroughputSampler throughput,
        Func<FakeGpuUsageSampler> gpuSampler) {
        var samplers = new MetricSamplers(
            () => 0, () => new MemorySample(0, 0, 0, 0, 0), () => new NetworkSample(0, 0), () => "TestNIC");
        var viewModel = new PerformanceViewModel(
            new SystemMetricsService(samplers, () => new FakeUiTimer()),
            StubHardwareProviders.Compose(
                gpuAdapters: () => Task.FromResult<IReadOnlyList<GpuAdapter>>(gpus.ToArray()),
                disks: () => Task.FromResult<IReadOnlyList<PhysicalDiskInfo>>(disks.ToArray()),
                volumes: () => Task.FromResult(Volumes())),
            gpuSampler, throughputSampler: throughput);

        // The rail otherwise lists only the primary disk and GPU.
        viewModel.ShowAllDevices = true;
        await Task.Delay(100);
        await viewModel.LoadInventoryAsync();
        return viewModel;
    }

    private static int PointCount(string points) =>
        points.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

    private static ResourceRow DiskRow(PerformanceViewModel page, int number) =>
        page.Resources.Single(r => r.DeviceId == DeviceIds.Disk(number));

    private static ResourceRow GpuRow(PerformanceViewModel page) =>
        page.Resources.Single(r => r.Series == ChartSeries.Gpu);

    [Fact]
    public async Task LoadInventoryAsync_Reload_KeepsTheDiskHistory() {
        var page = await LoadedAsync([Boot], [], new FakeDiskThroughputSampler().Reporting(Boot.DeviceId, 40),
                                     () => new FakeGpuUsageSampler());
        await page.LoadInventoryAsync();
        var before = PointCount(DiskRow(page, Boot.DeviceId).Points);

        await page.LoadInventoryAsync();

        Assert.True(before >= 2);
        Assert.Equal(before + 1, PointCount(DiskRow(page, Boot.DeviceId).Points));
    }

    [Fact]
    public async Task LoadInventoryAsync_Reload_KeepsTheGpuHistory() {
        var page = await LoadedAsync([], [Adapter(Amd, "AMD amdgpu (1002:73df)")], new FakeDiskThroughputSampler(),
                                     () => new FakeGpuUsageSampler().Reporting(Amd, 37));
        page.UpdateGpuAdapters();
        page.UpdateGpuAdapters();
        var before = PointCount(GpuRow(page).Points);

        await page.LoadInventoryAsync();

        Assert.True(before >= 3);
        Assert.Equal(before + 1, PointCount(GpuRow(page).Points));
    }

    /// <summary>The Detailed grid is rebuilt with the row too, and its charts are discovered lazily, so
    /// dropping them would also drop the toggle until the next tick found the engines again.</summary>
    [Fact]
    public async Task LoadInventoryAsync_Reload_KeepsTheGpuEngineCharts() {
        var engines = new Dictionary<string, double> { ["3d"] = 60, ["copy"] = 5 };
        var page = await LoadedAsync([], [Adapter(Amd, "AMD amdgpu (1002:73df)")], new FakeDiskThroughputSampler(),
                                     () => new FakeGpuUsageSampler().Reporting(Amd, 60, engines));
        page.UpdateGpuAdapters();
        page.UpdateGpuAdapters();
        var before = PointCount(GpuRow(page).SubCharts.First().Points);

        await page.LoadInventoryAsync();

        var row = GpuRow(page);
        Assert.True(row.SupportsDetail);
        Assert.Equal(2, row.SubCharts.Count);
        Assert.True(before >= 3);
        Assert.Equal(before + 1, PointCount(row.SubCharts.First().Points));
    }

    [Fact]
    public async Task LoadInventoryAsync_DiskNewOnReload_StartsWithOnlyItsSeed() {
        var disks = new List<PhysicalDiskInfo> { Boot };
        var throughput = new FakeDiskThroughputSampler()
            .Reporting(Boot.DeviceId, 40).Reporting(Data.DeviceId, 10);
        var page = await LoadedAsync(disks, [], throughput, () => new FakeGpuUsageSampler());
        await page.LoadInventoryAsync();
        var bootBefore = PointCount(DiskRow(page, Boot.DeviceId).Points);

        disks.Add(Data);
        await page.LoadInventoryAsync();

        Assert.Equal(bootBefore + 1, PointCount(DiskRow(page, Boot.DeviceId).Points));
        Assert.Equal(1, PointCount(DiskRow(page, Data.DeviceId).Points));
    }

    [Fact]
    public async Task LoadInventoryAsync_GpuNewOnReload_StartsWithOnlyItsSeed() {
        var gpus = new List<GpuAdapter> { Adapter(Amd, "AMD amdgpu (1002:73df)") };
        var page = await LoadedAsync([], gpus, new FakeDiskThroughputSampler(),
                                     () => new FakeGpuUsageSampler().Reporting(Amd, 37).Reporting(Nvidia, 80));
        await page.LoadInventoryAsync();

        gpus.Add(Adapter(Nvidia, "NVIDIA GeForce"));
        await page.LoadInventoryAsync();

        var counts = page.Resources.Where(r => r.Series == ChartSeries.Gpu)
            .Select(r => PointCount(r.Points)).Order().ToArray();
        Assert.Equal(2, counts.Length);
        Assert.Equal(1, counts[0]);
        Assert.True(counts[1] >= 2);
    }

    /// <summary>A device that leaves and comes back is a new device: nothing kept its history while it was
    /// gone, which is what stops a removed drive's trace from living on in the dictionary.</summary>
    [Fact]
    public async Task LoadInventoryAsync_DiskThatLeavesAndReturns_StartsFresh() {
        var disks = new List<PhysicalDiskInfo> { Boot, Data };
        var throughput = new FakeDiskThroughputSampler()
            .Reporting(Boot.DeviceId, 40).Reporting(Data.DeviceId, 10);
        var page = await LoadedAsync(disks, [], throughput, () => new FakeGpuUsageSampler());
        await page.LoadInventoryAsync();

        disks.Remove(Data);
        await page.LoadInventoryAsync();
        disks.Add(Data);
        await page.LoadInventoryAsync();

        Assert.Equal(1, PointCount(DiskRow(page, Data.DeviceId).Points));
    }
}
