using DashDetective.Services.Settings;
using DashDetective.Services.SystemMetrics;
using DashDetective.Shared;
using DashDetective.Tabs.Storage;
using DashDetective.Tests.Fakes;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace DashDetective.Tests.Tabs.Storage;

/// <summary>Covers the low-free-space bell on the Storage page: the page exposes the shell's own
/// free-space switch, every drive card carries that one switch, and the master switch off leaves it
/// unavailable.</summary>
public class StorageAlertTests {
    private static readonly IReadOnlyList<PhysicalDiskInfo> TwoDisks = [
        new(0, "Data Drive", "HDD", 2_000_000_000_000, true),
        new(1, "Boot Drive", "NVMe SSD", 1_000_000_000_000, true),
    ];

    private static IReadOnlyList<VolumeInfo> Volumes() => [
        new(0, 'D', "Data", "NTFS", 2_000_000_000_000, 1_000_000_000_000),
        new(1, SystemDrive.Letter, "", "NTFS", 1_000_000_000_000, 500_000_000_000),
    ];

    private static async Task<StorageViewModel> LoadedAsync(AlertSwitches alerts) {
        var page = new StorageViewModel(
            TestMetrics.Idle(), StubHardwareProviders.With(disks: TwoDisks, volumes: Volumes()), alerts);
        await page.LoadStorageAsync();
        return page;
    }

    [Fact]
    public async Task EveryDriveCard_CarriesTheSharedFreeSpaceSwitch() {
        var alerts = new AlertSwitches();
        var page = await LoadedAsync(alerts);

        Assert.Same(alerts.DiskSpace, page.LowDiskSpaceAlert);
        Assert.Equal(2, page.Drives.Count);
        Assert.All(page.Drives, card => Assert.Same(alerts.DiskSpace, card.Alert));
    }

    [Fact]
    public async Task FlippingACardsBell_IsCapturedUnderTheExistingKey() {
        var alerts = new AlertSwitches();
        var page = await LoadedAsync(alerts);

        page.Drives[0].Alert!.IsOn = false;

        Assert.False(alerts.CaptureInto(AppSettings.Defaults).AlertLowDiskFreeEnabled);
    }

    [Fact]
    public async Task MasterOff_LeavesTheBellUnavailable() {
        var alerts = new AlertSwitches();
        alerts.Reflect(AppSettings.Defaults with { ResourceAlerts = true });
        var page = await LoadedAsync(alerts);
        Assert.True(page.LowDiskSpaceAlert.IsAvailable);

        alerts.Reflect(AppSettings.Defaults with { ResourceAlerts = false });

        Assert.False(page.LowDiskSpaceAlert.IsAvailable);
    }
}
