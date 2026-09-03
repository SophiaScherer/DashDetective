using DashDetective.Services.SystemMetrics;
using DashDetective.Tabs.Dashboard;
using DashDetective.Tests.Fakes;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace DashDetective.Tests.Tabs.Dashboard;

/// <summary>Covers the Dashboard's jump to Performance: each chart and each card names the device it is
/// showing, by the same inventory id the Performance rail keys on. This page says nothing about which tab
/// that means — only the shell knows — so what is pinned here is the identity it raises.</summary>
public class DashboardOpenInPerformanceTests {
    private const int DiskNumber = 2;

    private static readonly IReadOnlyList<PhysicalDiskInfo> OneDisk = [
        new(DiskNumber, "Data Drive", "NVMe SSD", 1_000_000_000_000, false),
    ];

    private static IReadOnlyList<VolumeInfo> Volumes() => [
        new(DiskNumber, 'D', "Data", "NTFS", 1_000_000_000_000, 400_000_000_000),
    ];

    private static DashboardViewModel Page(HardwareProviders? providers = null) =>
        new(TestMetrics.Idle(), providers ?? StubHardwareProviders.With());

    private static string? Asked(DashboardViewModel page, System.Action act) {
        string? asked = null;
        page.PerformanceRevealRequested += id => asked = id;
        act();
        return asked;
    }

    [Fact]
    public void TheCpuChart_AsksForTheCpu() {
        var page = Page();

        Assert.Equal(DeviceIds.Cpu, Asked(page, () => page.OpenCpuChartCommand.Execute(null)));
    }

    [Fact]
    public void TheMemoryChart_AsksForMemory() {
        var page = Page();

        Assert.Equal(DeviceIds.Memory, Asked(page, () => page.OpenMemoryChartCommand.Execute(null)));
    }

    [Fact]
    public void TheNetworkChart_AsksForTheAdapter() {
        var page = Page();

        Assert.Equal(DeviceIds.Network, Asked(page, () => page.OpenNetworkChartCommand.Execute(null)));
    }

    [Theory]
    [InlineData(DeviceCategory.Cpu, "cpu")]
    [InlineData(DeviceCategory.Memory, "mem")]
    [InlineData(DeviceCategory.Network, "net")]
    public void ASingletonCard_AsksForItsOwnDevice(DeviceCategory category, string expected) {
        var page = Page();
        var card = page.Cards.First(c => c.Category == category);

        Assert.Equal(expected, Asked(page, () => card.OpenCommand.Execute(null)));
        Assert.Equal(expected, card.DeviceId);
    }

    [Fact]
    public async Task ADiskCard_AsksForThatDisk() {
        var page = Page(StubHardwareProviders.With(disks: OneDisk, volumes: Volumes()));
        await page.LoadDisksAsync();
        var card = page.Cards.First(c => c.Category == DeviceCategory.Disk);

        Assert.Equal(DeviceIds.Disk(DiskNumber),
                     Asked(page, () => card.OpenCommand.Execute(null)));
    }

    [Fact]
    public void ACardWithNothingToExplain_OffersTheOpenHintAsItsTooltip() {
        var page = Page();
        var card = page.Cards.First(c => c.Category == DeviceCategory.Cpu);

        Assert.Equal(DashboardCard.OpenHint, card.Tip);

        card.Note = "Why this reads —";
        Assert.Equal("Why this reads —", card.Tip);
    }
}
