using DashDetective.Services.Network;
using DashDetective.Services.SystemMetrics;
using DashDetective.Services.Theming;
using DashDetective.Shared;
using DashDetective.Tabs.Performance;
using DashDetective.Tests.Fakes;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace DashDetective.Tests.Tabs.Performance;

/// <summary>Covers <see cref="PerformanceViewModel.Reveal"/>, the jump into this page: an inventory id
/// selects its own row, a device the Primary scope hides brings the rail with it, and a reveal that
/// arrives before the rows are built waits for them. Also pins what makes that last one hold — a rebuilt
/// rail keeps the selected device, which it could not do while rows were matched by reference.</summary>
public class PerformanceRevealTests {
    private const int FirstDisk = 0;
    private const int SecondDisk = 3;

    private static readonly IReadOnlyList<PhysicalDiskInfo> TwoDisks = [
        new(FirstDisk, "Boot Drive", "NVMe SSD", 1_000_000_000_000, true),
        new(SecondDisk, "Data Drive", "NVMe SSD", 2_000_000_000_000, false),
    ];

    private static IReadOnlyList<VolumeInfo> Volumes() => [
        new(FirstDisk, SystemDrive.Letter, "", "NTFS", 1_000_000_000_000, 500_000_000_000),
    ];

    private static PerformanceViewModel Page() {
        var samplers = new MetricSamplers(
            () => 0, () => new MemorySample(0, 0, 0, 0, 0), () => new NetworkSample(0, 0), () => "TestNIC");

        return new PerformanceViewModel(
            new SystemMetricsService(samplers, () => new FakeUiTimer()),
            StubHardwareProviders.With(disks: TwoDisks, volumes: Volumes()),
            () => new FakeGpuUsageSampler());
    }

    private static async Task<PerformanceViewModel> LoadedAsync() {
        var page = Page();
        // The constructor fires its own load and forgets it; letting that finish keeps the row set stable.
        await Task.Delay(100);
        await page.LoadInventoryAsync();
        return page;
    }

    [Fact]
    public async Task Reveal_TheCpuId_SelectsTheCpuRow() {
        var page = await LoadedAsync();
        page.Reveal(DeviceIds.Memory);

        page.Reveal(DeviceIds.Cpu);

        Assert.Equal(ChartSeries.Cpu, page.SelectedResource.Series);
        Assert.True(page.SelectedResource.IsSelected);
    }

    [Theory]
    [InlineData("mem", ChartSeries.Memory)]
    [InlineData("net", ChartSeries.NetDown)]
    public async Task Reveal_ASingletonId_SelectsThatRow(string deviceId, ChartSeries expected) {
        var page = await LoadedAsync();

        page.Reveal(deviceId);

        Assert.Equal(expected, page.SelectedResource.Series);
    }

    [Fact]
    public async Task Reveal_ADiskHiddenByThePrimaryScope_ExpandsTheRailAndSelectsIt() {
        var page = await LoadedAsync();
        Assert.False(page.ShowAllDevices);
        Assert.DoesNotContain(page.Resources, row => row.DeviceId == DeviceIds.Disk(SecondDisk));

        page.Reveal(DeviceIds.Disk(SecondDisk));

        Assert.True(page.ShowAllDevices);
        Assert.Equal(DeviceIds.Disk(SecondDisk), page.SelectedResource.DeviceId);
    }

    [Fact]
    public async Task Reveal_AVisibleDisk_LeavesTheScopeAlone() {
        var page = await LoadedAsync();

        page.Reveal(DeviceIds.Disk(FirstDisk));

        Assert.False(page.ShowAllDevices);
        Assert.Equal(DeviceIds.Disk(FirstDisk), page.SelectedResource.DeviceId);
    }

    [Fact]
    public async Task Reveal_BeforeTheInventoryLoads_LandsOnceTheRowsExist() {
        var page = Page();

        // The disk rows do not exist yet, so the request has nothing to point at.
        page.Reveal(DeviceIds.Disk(SecondDisk));
        Assert.DoesNotContain(page.Resources, row => row.DeviceId == DeviceIds.Disk(SecondDisk));

        await Task.Delay(100);
        await page.LoadInventoryAsync();

        Assert.Equal(DeviceIds.Disk(SecondDisk), page.SelectedResource.DeviceId);
    }

    [Fact]
    public async Task ASelectedDisk_SurvivesAnInventoryReload() {
        var page = await LoadedAsync();
        page.Reveal(DeviceIds.Disk(SecondDisk));

        // What the toolbar Refresh does. The rows come back as new objects.
        await page.LoadInventoryAsync();

        Assert.Equal(DeviceIds.Disk(SecondDisk), page.SelectedResource.DeviceId);
    }

    [Fact]
    public async Task Reveal_AnUnknownId_ChangesNothing() {
        var page = await LoadedAsync();
        var selected = page.SelectedResource;

        page.Reveal("disk:404");
        page.Reveal("");

        Assert.Same(selected, page.SelectedResource);
        Assert.False(page.ShowAllDevices);
    }

    [Fact]
    public async Task EveryRailRow_CarriesTheInventoryIdItWasBuiltFrom() {
        var page = await LoadedAsync();
        page.ShowAllDevices = true;

        Assert.All(page.Resources, row => Assert.False(string.IsNullOrEmpty(row.DeviceId)));
        Assert.Equal(page.Resources.Count, page.Resources.Select(row => row.DeviceId).Distinct().Count());
    }
}
