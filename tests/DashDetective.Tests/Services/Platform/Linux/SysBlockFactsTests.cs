using DashDetective.Services.Platform.Linux;
using DashDetective.Services.SystemMetrics;
using DashDetective.Tests.Fakes;
using System.Linq;
using Xunit;

namespace DashDetective.Tests.Services.Platform.Linux;

/// <summary>Covers <see cref="SysBlockFacts"/>: the device filter that is the Storage milestone's whole
/// acceptance criterion, the packed <c>major:minor</c> identity three separate readers have to agree on,
/// the partition→disk map, and the mapper resolution that keeps an LVM root on a real drive.</summary>
public class SysBlockFactsTests {
    private static SysBlockFacts Read(FakeProcFileSystem proc) => SysBlockFacts.Read(proc);

    private static FakeProcFileSystem VirtualBox() =>
        new FakeProcFileSystem().WithVirtualBoxBlockTree();

    /// <summary>The milestone's stated acceptance criterion: a stock Ubuntu GNOME install has ~25 snap
    /// loop devices, and without this filter the Storage tab is unusable and looks broken. Optical drives
    /// go the same way.</summary>
    [Fact]
    public void Read_ExcludesLoopAndOpticalDevices() {
        var names = Read(VirtualBox()).Disks.Select(d => d.Name).ToList();

        Assert.Equal(["sda"], names);
    }

    [Theory]
    [InlineData("ram0")]
    [InlineData("zram0")]
    [InlineData("sr1")]
    [InlineData("loop0")]
    public void Read_ExcludesEveryVirtualDevicePrefix(string name) {
        var proc = new FakeProcFileSystem()
            .WithFile($"/sys/block/{name}/dev", "1:0\n")
            .WithFile($"/sys/block/{name}/size", "1024\n");

        Assert.Empty(Read(proc).Disks);
    }

    /// <summary>The join key for the whole Storage surface: the kernel's own <c>major:minor</c>, packed the
    /// way its 32-bit <c>dev_t</c> does. <c>sda</c> is 8:0.</summary>
    [Fact]
    public void Read_KeysADiskByItsPackedDeviceNumber() =>
        Assert.Equal((8 << 20) | 0, Read(VirtualBox()).Disks[0].DiskNumber);

    /// <summary>512-byte sectors regardless of the drive's physical sector size — reading the file as bytes
    /// would under-report every drive by a factor of 512.</summary>
    [Fact]
    public void Read_ConvertsSizeFromFiveHundredAndTwelveByteSectors() =>
        Assert.Equal(41943040UL * 512, Read(VirtualBox()).Disks[0].SizeBytes);

    [Fact]
    public void Read_TakesTheModelFromTheDeviceDirectory() =>
        Assert.Equal("VBOX HARDDISK", Read(VirtualBox()).Disks[0].Model);

    /// <summary>A virtio disk fills neither field; "" is reported honestly so each consumer applies its own
    /// placeholder rather than this inventing one.</summary>
    [Fact]
    public void Read_WithNoModelOrVendor_ReportsNoModel() {
        var proc = new FakeProcFileSystem()
            .WithFile("/sys/block/vda/dev", "253:0\n")
            .WithFile("/sys/block/vda/size", "1024\n");

        Assert.Equal("", Read(proc).Disks[0].Model);
    }

    [Fact]
    public void Read_ReadsTheKindFromTheRotationalFlag() =>
        Assert.Equal(DriveKind.Hdd, Read(VirtualBox()).Disks[0].Kind);

    /// <summary>NVMe is a bus fact the rotational flag cannot carry, so it comes from the name — and it
    /// outranks the flag, matching how the WMI arm ranks the bus over the media type.</summary>
    [Fact]
    public void Read_NamesAnNvmeNamespaceNvmeRatherThanSsd() {
        var proc = new FakeProcFileSystem()
            .WithFile("/sys/block/nvme0n1/dev", "259:0\n")
            .WithFile("/sys/block/nvme0n1/size", "1024\n")
            .WithFile("/sys/block/nvme0n1/queue/rotational", "0\n");

        Assert.Equal(DriveKind.Nvme, Read(proc).Disks[0].Kind);
    }

    /// <summary>A device with no capacity is absent media (an empty card reader), not a drive.</summary>
    [Fact]
    public void Read_SkipsADeviceReportingNoCapacity() {
        var proc = new FakeProcFileSystem()
            .WithFile("/sys/block/sdc/dev", "8:32\n")
            .WithFile("/sys/block/sdc/size", "0\n");

        Assert.Empty(Read(proc).Disks);
    }

    /// <summary>The kernel nests partitions inside their disk, so the map falls out of the same walk — no
    /// symlink resolution needed. Both partitions answer with the disk's number, not their own.</summary>
    [Fact]
    public void DiskNumberFor_MapsAPartitionToItsDisk() {
        var facts = Read(VirtualBox());
        var sda = facts.Disks[0].DiskNumber;

        Assert.Equal(sda, facts.DiskNumberFor("sda1"));
        Assert.Equal(sda, facts.DiskNumberFor("sda2"));
        Assert.Equal(sda, facts.DiskNumberFor("sda"));
    }

    /// <summary>A snap's loop device is filtered out, so a mount on it resolves to nothing and the caller
    /// drops it — which is what keeps the volume list free of the loop flood too.</summary>
    [Fact]
    public void DiskNumberFor_AnExcludedDevice_ResolvesToNothing() =>
        Assert.Null(Read(VirtualBox()).DiskNumberFor("loop3"));

    /// <summary>Ubuntu Server's default layout. A blanket <c>dm-*</c> filter would drop the root volume
    /// from the Storage tab entirely; following <c>slaves</c> files its capacity against the real
    /// drive.</summary>
    [Fact]
    public void DiskNumberFor_AMapperDevice_ResolvesThroughSlavesToItsBackingDisk() {
        var facts = Read(VirtualBox().WithLvmRoot());

        Assert.Equal(facts.Disks[0].DiskNumber, facts.DiskNumberFor("dm-0"));
    }

    /// <summary>A mapper device is not a disk in its own right — its capacity already belongs to the drive
    /// backing it, so counting both would double the machine's storage.</summary>
    [Fact]
    public void Read_DoesNotCountAMapperDeviceAsItsOwnDisk() =>
        Assert.Equal(["sda"], Read(VirtualBox().WithLvmRoot()).Disks.Select(d => d.Name));

    /// <summary>LUKS over LVM over a partition — the chain is followed rather than giving up at the first
    /// hop.</summary>
    [Fact]
    public void DiskNumberFor_AChainedMapperDevice_FollowsItToTheDisk() {
        var proc = VirtualBox().WithLvmRoot()
            .WithFile("/sys/block/dm-1/dev", "252:1\n")
            .WithFile("/sys/block/dm-1/slaves/dm-0", "");
        var facts = Read(proc);

        Assert.Equal(facts.Disks[0].DiskNumber, facts.DiskNumberFor("dm-1"));
    }

    /// <summary>Nothing readable is not a crash — an empty <c>/sys</c> yields the empty contract.</summary>
    [Fact]
    public void Read_WithNoSysBlock_ReportsNothing() {
        var facts = Read(new FakeProcFileSystem());

        Assert.Empty(facts.Disks);
        Assert.Null(facts.DiskNumberFor("sda"));
    }

    /// <summary>The cheap half the throughput sampler calls every tick has to agree with the full read,
    /// or the sampler would report disks the cards do not have.</summary>
    [Fact]
    public void DiskNumbers_MatchesTheDisksTheFullReadReports() {
        var proc = VirtualBox().WithLvmRoot();

        Assert.Equal(
            Read(proc).Disks.Select(d => d.DiskNumber).ToHashSet(),
            SysBlockFacts.DiskNumbers(proc));
    }
}
