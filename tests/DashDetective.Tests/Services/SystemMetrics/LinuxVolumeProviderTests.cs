using DashDetective.Services.Platform.Linux;
using DashDetective.Services.SystemMetrics;
using DashDetective.Tests.Fakes;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace DashDetective.Tests.Services.SystemMetrics;

/// <summary>Covers <see cref="LinuxVolumeProvider"/>: the one rule that filters the pseudo-filesystem and
/// snap-loop floods, the dedupe that stops a repeated device multiplying a drive's capacity, and the
/// mapper resolution that keeps an LVM root visible.</summary>
public class LinuxVolumeProviderTests {
    /// <summary>Reports a fixed size for any mount point, so a test asserting on the filter and the dedupe
    /// does not also depend on the dev box's real filesystems.</summary>
    private sealed class StubCapacityReader(ulong size = 1024, ulong free = 512) : IVolumeCapacityReader {
        public List<string> Reads { get; } = [];

        public VolumeCapacity Read(string mountPoint) {
            Reads.Add(mountPoint);
            return new VolumeCapacity(size, free);
        }
    }

    private static Task<IReadOnlyList<VolumeInfo>> ReadAsync(
        FakeProcFileSystem proc, IVolumeCapacityReader? capacity = null) =>
        new LinuxVolumeProvider(proc, capacity ?? new StubCapacityReader()).GetAsync();

    private static FakeProcFileSystem Ubuntu() =>
        new FakeProcFileSystem()
            .WithVirtualBoxBlockTree()
            .WithFile("/proc/mounts", ProcFixtures.ProcMounts);

    /// <summary>The one rule doing all the filtering: a mount is kept only when its device resolves to a
    /// disk that has a card. The fixture's tmpfs/proc/cgroup lines name no device and its two snap mounts
    /// resolve to excluded loop devices, so only the two real partitions survive.</summary>
    [Fact]
    public async Task GetAsync_KeepsOnlyMountsBackedByARealDisk() {
        var volumes = await ReadAsync(Ubuntu());

        Assert.Equal(["/", "/boot/efi"], volumes.Select(v => v.MountPoint).Order().ToList());
    }

    /// <summary>The Storage tab's whole acceptance criterion, seen from the volume side.</summary>
    [Fact]
    public async Task GetAsync_DropsSnapLoopMounts() =>
        Assert.DoesNotContain(
            await ReadAsync(Ubuntu()), v => v.MountPoint.StartsWith("/snap", System.StringComparison.Ordinal));

    /// <summary>The fixture lists <c>/dev/sda2</c> twice — as the root and again as a bind mount. The drive
    /// cards sum their volumes' sizes, so a duplicate would double the drive's capacity and its used space.
    /// The shortest mount point wins, being the one a user thinks of as the volume.</summary>
    [Fact]
    public async Task GetAsync_CollapsesARepeatedDeviceToItsShallowestMount() {
        var volumes = await ReadAsync(Ubuntu());

        Assert.Single(volumes, v => v.MountPoint == "/");
        Assert.DoesNotContain(volumes, v => v.MountPoint == "/var/lib/docker/btrfs");
    }

    [Fact]
    public async Task GetAsync_FilesEachVolumeAgainstItsHostDisk() {
        var proc = Ubuntu();
        var expected = SysBlockFacts.Read(proc).Disks[0].DiskNumber;

        Assert.All(await ReadAsync(proc), v => Assert.Equal(expected, v.DiskNumber));
    }

    [Fact]
    public async Task GetAsync_ReadsTheFilesystemAndCapacity() {
        var root = (await ReadAsync(Ubuntu(), new StubCapacityReader(2048, 1024)))
            .Single(v => v.MountPoint == "/");

        Assert.Equal("ext4", root.FileSystem);
        Assert.Equal(2048UL, root.SizeBytes);
        Assert.Equal(1024UL, root.FreeBytes);
    }

    /// <summary>A volume that cannot be measured is not a volume worth a row — the drive card would count
    /// it as zero capacity and drag the rollup down.</summary>
    [Fact]
    public async Task GetAsync_DropsAVolumeThatReportsNoCapacity() =>
        Assert.Empty(await ReadAsync(Ubuntu(), new StubCapacityReader(0, 0)));

    /// <summary>Linux volumes carry no drive letter and no GPT type; both stay empty rather than being
    /// invented, and the Partitions table renders them from the mount point instead.</summary>
    [Fact]
    public async Task GetAsync_ReportsNoDriveLetterOrGptType() =>
        Assert.All(await ReadAsync(Ubuntu()), v => {
            Assert.Null(v.DriveLetter);
            Assert.Equal("", v.GptType);
        });

    /// <summary>Ubuntu Server's default: the root is named through <c>/dev/mapper</c>, a symlink to a
    /// <c>dm</c> node whose backing disk the block reader traced. Resolving the link is what stops the root
    /// volume disappearing from the tab.</summary>
    [Fact]
    public async Task GetAsync_ResolvesAMapperMountToItsBackingDisk() {
        var proc = new FakeProcFileSystem()
            .WithVirtualBoxBlockTree()
            .WithLvmRoot()
            .WithFile("/proc/mounts", "/dev/mapper/ubuntu--vg-ubuntu--lv / ext4 rw,relatime 0 0");

        var root = Assert.Single(await ReadAsync(proc));
        Assert.Equal(SysBlockFacts.Read(proc).Disks[0].DiskNumber, root.DiskNumber);
    }

    /// <summary>udev publishes labels as symlinks named in hex escapes; the Partitions table's Label column
    /// would otherwise be empty on every Linux machine.</summary>
    [Fact]
    public async Task GetAsync_ReadsTheLabelFromTheByLabelSymlinks() {
        var proc = Ubuntu().WithLink(@"/dev/disk/by-label/My\x20Root", "/dev/sda2");

        Assert.Equal("My Root", (await ReadAsync(proc)).Single(v => v.MountPoint == "/").Label);
    }

    [Fact]
    public async Task GetAsync_WithNoMounts_ReportsNothing() =>
        Assert.Empty(await ReadAsync(new FakeProcFileSystem().WithVirtualBoxBlockTree()));

    /// <summary>Capacity is only measured for mounts that survive the filter — a reader that measured first
    /// would stat all ~30 pseudo-filesystems on every load.</summary>
    [Fact]
    public async Task GetAsync_DoesNotMeasureFilteredMounts() {
        var capacity = new StubCapacityReader();
        await ReadAsync(Ubuntu(), capacity);

        Assert.DoesNotContain(capacity.Reads, m => m.StartsWith("/snap", System.StringComparison.Ordinal));
        Assert.DoesNotContain("/run", capacity.Reads);
    }
}
