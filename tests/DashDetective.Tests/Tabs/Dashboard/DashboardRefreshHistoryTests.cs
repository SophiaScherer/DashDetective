using DashDetective.Services.SystemMetrics;
using DashDetective.Shared;
using DashDetective.Tabs.Dashboard;
using DashDetective.Tests.Fakes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace DashDetective.Tests.Tabs.Dashboard;

/// <summary>
/// Pins that the toolbar Refresh, which rebuilds the GPU and disk cards, carries each surviving device's
/// accumulated chart history over rather than restarting it from empty. A rebuilt card seeds one sample of
/// its own, so a surviving trace grows by exactly that one; a device new on the reload has only that seed.
/// </summary>
public class DashboardRefreshHistoryTests {
    private const string Amd = "0000:03:00.0";
    private const string Nvidia = "0000:01:00.0";

    private static readonly PhysicalDiskInfo Boot = new(3, "Boot Drive", "NVMe SSD", 1_000_000_000_000, true);
    private static readonly PhysicalDiskInfo Data = new(4, "Data Drive", "NVMe SSD", 1_000_000_000_000, false);

    private static GpuAdapter Adapter(string key, string name) => new(key, name, false, 0);

    private static IReadOnlyList<VolumeInfo> Volumes() => [
        new(Boot.DeviceId, SystemDrive.Letter, "", "NTFS", 1_000_000_000_000, 500_000_000_000),
    ];

    /// <summary>The page over mutable device lists, so a test can change what a reload finds. Waits out the
    /// loads the constructor fires and forgets, so every later count is deterministic.</summary>
    private static async Task<DashboardViewModel> LoadedAsync(
        List<PhysicalDiskInfo> disks, List<GpuAdapter> gpus, FakeDiskThroughputSampler throughput,
        Func<FakeGpuUsageSampler> gpuSampler) {
        var viewModel = new DashboardViewModel(
            TestMetrics.Idle(),
            StubHardwareProviders.Compose(
                gpuAdapters: () => Task.FromResult<IReadOnlyList<GpuAdapter>>(gpus.ToArray()),
                disks: () => Task.FromResult<IReadOnlyList<PhysicalDiskInfo>>(disks.ToArray()),
                volumes: () => Task.FromResult(Volumes())),
            gpuSampler, throughput);

        await Task.Delay(100);
        await viewModel.LoadGpusAsync();
        await viewModel.LoadDisksAsync();
        return viewModel;
    }

    private static int PointCount(string points) =>
        points.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

    private static DashboardCard DiskCard(DashboardViewModel page, int number) =>
        page.Cards.Single(c => c.DeviceId == DeviceIds.Disk(number));

    private static IEnumerable<DashboardCard> GpuCards(DashboardViewModel page) =>
        page.Cards.Where(c => c.Category == DeviceCategory.Gpu);

    [Fact]
    public async Task LoadDisksAsync_Reload_KeepsTheDiskHistory() {
        var page = await LoadedAsync([Boot], [], new FakeDiskThroughputSampler().Reporting(Boot.DeviceId, 40),
                                     () => new FakeGpuUsageSampler());
        var before = PointCount(DiskCard(page, Boot.DeviceId).Points);

        await page.LoadDisksAsync();

        Assert.True(before >= 2);
        Assert.Equal(before + 1, PointCount(DiskCard(page, Boot.DeviceId).Points));
    }

    [Fact]
    public async Task LoadGpusAsync_Reload_KeepsTheGpuHistory() {
        var page = await LoadedAsync([], [Adapter(Amd, "AMD amdgpu (1002:73df)")], new FakeDiskThroughputSampler(),
                                     () => new FakeGpuUsageSampler().Reporting(Amd, 37));
        page.UpdateGpuAdapters();
        var before = PointCount(GpuCards(page).Single().Points);

        await page.LoadGpusAsync();

        Assert.True(before >= 3);
        Assert.Equal(before + 1, PointCount(GpuCards(page).Single().Points));
    }

    /// <summary>An adapter that reports nothing this tick still shows the trace it has behind it, rather
    /// than a chart blanked by the rebuild until the sampler next answers.</summary>
    [Fact]
    public async Task LoadGpusAsync_Reload_DrawsTheCarriedTraceBeforeTheNextTick() {
        // The page keeps the first sampler the factory mints; each inventory load mints (and disposes) another.
        FakeGpuUsageSampler? sampler = null;
        var page = await LoadedAsync([], [Adapter(Amd, "AMD amdgpu (1002:73df)")], new FakeDiskThroughputSampler(),
                                     () => {
                                         var minted = new FakeGpuUsageSampler().Reporting(Amd, 37);
                                         sampler ??= minted;
                                         return minted;
                                     });
        page.UpdateGpuAdapters();
        var before = PointCount(GpuCards(page).Single().Points);

        sampler!.Dispose();
        await page.LoadGpusAsync();

        Assert.Equal(before, PointCount(GpuCards(page).Single().Points));
    }

    [Fact]
    public async Task LoadDisksAsync_DiskNewOnReload_StartsWithOnlyItsSeed() {
        var disks = new List<PhysicalDiskInfo> { Boot };
        var throughput = new FakeDiskThroughputSampler()
            .Reporting(Boot.DeviceId, 40).Reporting(Data.DeviceId, 10);
        var page = await LoadedAsync(disks, [], throughput, () => new FakeGpuUsageSampler());
        var bootBefore = PointCount(DiskCard(page, Boot.DeviceId).Points);

        disks.Add(Data);
        await page.LoadDisksAsync();

        Assert.Equal(bootBefore + 1, PointCount(DiskCard(page, Boot.DeviceId).Points));
        Assert.Equal(1, PointCount(DiskCard(page, Data.DeviceId).Points));
    }

    [Fact]
    public async Task LoadGpusAsync_GpuNewOnReload_StartsWithOnlyItsSeed() {
        var gpus = new List<GpuAdapter> { Adapter(Amd, "AMD amdgpu (1002:73df)") };
        var page = await LoadedAsync([], gpus, new FakeDiskThroughputSampler(),
                                     () => new FakeGpuUsageSampler().Reporting(Amd, 37).Reporting(Nvidia, 80));
        await page.LoadGpusAsync();

        gpus.Add(Adapter(Nvidia, "NVIDIA GeForce"));
        await page.LoadGpusAsync();

        var counts = GpuCards(page).Select(c => PointCount(c.Points)).Order().ToArray();
        Assert.Equal(2, counts.Length);
        Assert.Equal(1, counts[0]);
        Assert.True(counts[1] >= 2);
    }
}
