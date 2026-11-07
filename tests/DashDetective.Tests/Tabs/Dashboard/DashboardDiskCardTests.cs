using DashDetective.Services.SystemMetrics;
using DashDetective.Shared;
using DashDetective.Tabs.Dashboard;
using DashDetective.Tests.Fakes;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace DashDetective.Tests.Tabs.Dashboard;

/// <summary>Pins that a disk card says "—" until its sampler reports, and keeps saying it if it never does; a real 0 % still shows.</summary>
public class DashboardDiskCardTests {
    private static readonly PhysicalDiskInfo Boot = new(3, "Boot Drive", "NVMe SSD", 1_000_000_000_000, true);

    private static IReadOnlyList<VolumeInfo> Volumes() => [
        new(Boot.DeviceId, SystemDrive.Letter, "", "NTFS", 1_000_000_000_000, 500_000_000_000),
    ];

    /// <summary>Waits out the constructor's fire-and-forget loads, then drives one disk load.</summary>
    private static async Task<DashboardCard> DiskCardAsync(FakeDiskThroughputSampler throughput) {
        var viewModel = new DashboardViewModel(
            TestMetrics.Idle(),
            StubHardwareProviders.With(disks: [Boot], volumes: Volumes()),
            () => new FakeGpuUsageSampler(), throughput);

        await Task.Delay(100);
        await viewModel.LoadDisksAsync();
        return viewModel.Cards.Single(c => c.Category == DeviceCategory.Disk);
    }

    [Fact]
    public async Task LoadDisksAsync_SamplerReportsNothing_ShowsThePlaceholder() {
        var card = await DiskCardAsync(new FakeDiskThroughputSampler());

        Assert.Equal(Placeholders.NoReading, card.Value);
        // The unit goes with it, so the card reads "—" and not "— %".
        Assert.Equal("", card.Unit);
    }

    [Fact]
    public async Task LoadDisksAsync_DiskReportingZero_ShowsZeroPercent() {
        var card = await DiskCardAsync(new FakeDiskThroughputSampler().Reporting(Boot.DeviceId, 0));

        Assert.Equal("0", card.Value);
        Assert.Equal("%", card.Unit);
    }

    [Fact]
    public async Task LoadDisksAsync_DiskReporting_ShowsTheValueAndItsUnit() {
        var card = await DiskCardAsync(new FakeDiskThroughputSampler().Reporting(Boot.DeviceId, 42));

        Assert.Equal("42", card.Value);
        Assert.Equal("%", card.Unit);
    }
}
