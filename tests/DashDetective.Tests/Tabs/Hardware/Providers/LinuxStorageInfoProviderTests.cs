using DashDetective.Tabs.Hardware;
using DashDetective.Tests.Fakes;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace DashDetective.Tests.Tabs.Hardware.Providers;

/// <summary>Covers <see cref="LinuxStorageInfoProvider"/>: the Storage Devices card it composes, its
/// terser wording beside the Storage tab's, and the health row that stays blank without root.</summary>
public class LinuxStorageInfoProviderTests {
    private static Task<StorageInfo> Read(FakeProcFileSystem proc) =>
        new LinuxStorageInfoProvider(proc).GetAsync();

    private static FakeProcFileSystem VirtualBox() =>
        new FakeProcFileSystem().WithVirtualBoxBlockTree();

    /// <summary>Decimal (marketing) units, from the shared formatter — 20 GiB of sectors is 21.5 GB.</summary>
    [Fact]
    public async Task GetAsync_SummarisesTheDriveCountAndCapacity() =>
        Assert.Equal("1 drive · 21.5 GB total", (await Read(VirtualBox())).Summary);

    [Fact]
    public async Task GetAsync_RendersOneRowPerDisk() {
        var drive = Assert.Single((await Read(VirtualBox())).Drives);

        Assert.Equal("VBOX HARDDISK", drive.Model);
        Assert.Equal("21.5 GB HDD", drive.Detail);
    }

    /// <summary>The loop flood must not reach this card any more than it reaches the Storage tab's.</summary>
    [Fact]
    public async Task GetAsync_ExcludesLoopAndOpticalDevices() =>
        Assert.Single((await Read(VirtualBox())).Drives);

    /// <summary>This card's wording is deliberately terser than the Storage tab's "NVMe SSD", but both
    /// read the same shared derivation, so they can never disagree about what the drive is.</summary>
    [Fact]
    public async Task GetAsync_UsesTheTerserSpecRowWording() {
        var proc = new FakeProcFileSystem()
            .WithFile("/sys/block/nvme0n1/dev", "259:0\n")
            .WithFile("/sys/block/nvme0n1/size", "3907029168\n")
            .WithFile("/sys/block/nvme0n1/queue/rotational", "0\n")
            .WithFile("/sys/block/nvme0n1/device/model", "Samsung SSD 980 PRO 2TB\n");

        Assert.Equal("2 TB NVMe", (await Read(proc)).Drives.Single().Detail);
    }

    /// <summary>Health folds <c>MSFT_PhysicalDisk</c>'s status on Windows; its Linux equivalent is SMART,
    /// which needs root. Blank rather than an overstated "Good".</summary>
    [Fact]
    public async Task GetAsync_LeavesHealthUnreportedWithoutRootSmart() =>
        Assert.Equal("—", (await Read(VirtualBox())).TotalHealth);

    [Fact]
    public async Task GetAsync_WithNoSysBlock_ReportsUnknown() =>
        Assert.Equal(StorageInfo.Unknown, await Read(new FakeProcFileSystem()));
}
