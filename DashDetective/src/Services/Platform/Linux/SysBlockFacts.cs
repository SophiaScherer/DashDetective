using DashDetective.Services.SystemMetrics;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace DashDetective.Services.Platform.Linux;

/// <summary>One physical disk as <c>/sys/block</c> describes it: its kernel name (<c>sda</c>,
/// <c>nvme0n1</c>), its packed device number, capacity, model and kind. <c>Model</c> is already resolved
/// from the model-then-vendor fallback so two consumers cannot render the same drive differently; "" means
/// the device reports neither, which each consumer replaces with its own placeholder.</summary>
internal readonly record struct BlockDeviceFacts(
    string Name, int DiskNumber, ulong SizeBytes, string Model, DriveKind Kind);

/// <summary>
/// The <c>/sys/block</c> picture, derived once and shared by everything that talks about a disk on Linux:
/// the Storage tab's drive cards (<c>LinuxPhysicalDiskProvider</c>), its Partitions table
/// (<c>LinuxVolumeProvider</c>) and the Hardware tab's Storage Devices card. The <see cref="CpuFacts"/>
/// precedent — where several cards want the same derived facts, the derivation is shared, not just the
/// parser, so they cannot disagree about the same hardware.
///
/// <b><see cref="DiskNumberFor"/> is the join key for the whole Storage surface.</b> The records these feed
/// (<c>PhysicalDiskInfo</c>, <c>VolumeInfo</c>, <c>DiskThroughputSample</c>) are keyed by an <c>int</c> disk
/// number, which on Windows is the OS's own. Linux has no such number, so this packs the kernel's
/// <c>major:minor</c> — the real identity of a block device, readable from both <c>/sys/block/*/dev</c> and
/// <c>/proc/diskstats</c>, so three independently-sampled providers derive the same key from the same
/// authority. A positional index would drift the moment a USB drive is plugged in mid-run.
///
/// Stateless and never throws: an unreadable <c>/sys</c> yields <see cref="None"/>.
/// </summary>
internal sealed record SysBlockFacts(
    IReadOnlyList<BlockDeviceFacts> Disks, IReadOnlyDictionary<string, int> DiskNumberByDevice) {

    // Concatenated forward-slash literals, never Path.Combine — see IProcFileSystem.
    private const string BlockRoot = "/sys/block";

    // A sector is 512 bytes in the `size` file regardless of the drive's physical sector size.
    private const int SectorBytes = 512;

    // Virtual and pseudo devices that would flood the Storage tab: a stock Ubuntu GNOME install has ~25
    // snap `loop` devices. `sr` is optical, `ram`/`zram` are memory-backed.
    private static readonly string[] ExcludedPrefixes = ["loop", "ram", "zram", "sr"];

    // Mapper and RAID devices resolve to whatever backs them rather than being counted as disks of their
    // own; the walk is capped because `slaves` can chain (LUKS over LVM over a partition).
    private const int MaxSlaveDepth = 4;

    /// <summary>Nothing readable — no <c>/sys/block</c>, or nothing in it that is a real disk.</summary>
    internal static SysBlockFacts None { get; } = new([], new Dictionary<string, int>(StringComparer.Ordinal));

    /// <summary>Reads and derives the facts. Never throws: an unreadable source yields
    /// <see cref="None"/>.</summary>
    internal static SysBlockFacts Read(IProcFileSystem proc) {
        var disks = new List<BlockDeviceFacts>();
        var numberByDevice = new Dictionary<string, int>(StringComparer.Ordinal);
        var aliases = new List<string>();

        foreach (var name in proc.ListDirectory(BlockRoot)) {
            if (IsExcluded(name))
                continue;

            // A device with slaves is a view onto other devices (dm-0 over sda3, md0 over sdb+sdc), not a
            // disk of its own — resolve it below, once the real disks are known.
            if (SlaveOf(proc, name) is not null) {
                aliases.Add(name);
                continue;
            }

            if (ReadDisk(proc, name) is not { } disk)
                continue;

            disks.Add(disk);
            numberByDevice[disk.Name] = disk.DiskNumber;
            foreach (var partition in Partitions(proc, disk.Name))
                numberByDevice[partition] = disk.DiskNumber;
        }

        foreach (var alias in aliases)
            if (ResolveAlias(proc, alias, numberByDevice) is { } number)
                numberByDevice[alias] = number;

        if (disks.Count == 0)
            return None;

        disks.Sort(static (a, b) => a.DiskNumber.CompareTo(b.DiskNumber));
        return new SysBlockFacts(disks, numberByDevice);
    }

    /// <summary>The disk number a device name belongs to — <c>sda</c> and <c>sda1</c> both give sda's,
    /// and a mapper device gives whichever disk backs it. <c>null</c> when the name is unknown or resolves
    /// to nothing, which is how a caller drops a mount on a filtered-out device.</summary>
    internal int? DiskNumberFor(string deviceName) =>
        DiskNumberByDevice.TryGetValue(deviceName, out var number) ? number : null;

    /// <summary>
    /// The disk numbers of the whole disks, without reading their model, size or kind — the cheap half of
    /// <see cref="Read"/>, for the throughput sampler, which runs on every tick and only needs to tell a
    /// disk from a partition. <c>/proc/diskstats</c> lists <c>sda</c> and <c>sda1</c> alike, and counting
    /// both double-counts the same I/O.
    /// </summary>
    internal static HashSet<int> DiskNumbers(IProcFileSystem proc) {
        var numbers = new HashSet<int>();

        foreach (var name in proc.ListDirectory(BlockRoot)) {
            if (IsExcluded(name) || SlaveOf(proc, name) is not null)
                continue;

            if (ParseDeviceNumber(proc.ReadAllText(BlockRoot + "/" + name + "/dev")) is { } number)
                numbers.Add(number);
        }

        return numbers;
    }

    /// <summary>
    /// The packed disk number of one named whole device, read straight from its own <c>dev</c> file — the
    /// single-read path for a caller that already knows the name and does not need the rest of the picture
    /// (the temperature reader, which arrives at a device name via a hwmon symlink). <c>null</c> when the
    /// device has gone away or reports no number.
    ///
    /// Whole devices only: a partition's <c>dev</c> file carries the <i>partition's</i> number, so this
    /// would not give sda's for sda1. Use <see cref="DiskNumberFor"/> on a full read for that.
    /// </summary>
    internal static int? DiskNumberOf(IProcFileSystem proc, string deviceName) =>
        ParseDeviceNumber(proc.ReadAllText(BlockRoot + "/" + deviceName + "/dev"));

    /// <summary>Packs a <c>major:minor</c> pair the way the kernel's 32-bit <c>dev_t</c> does. Majors are
    /// 12 bits and minors 20, so every real block device lands in a positive <c>int</c>. Shared with
    /// <see cref="ProcDiskstatsParser"/>, whose first two columns are the same pair.</summary>
    internal static int Pack(int major, int minor) => (major << 20) | minor;

    private static bool IsExcluded(string name) {
        foreach (var prefix in ExcludedPrefixes)
            if (name.StartsWith(prefix, StringComparison.Ordinal))
                return true;

        return false;
    }

    /// <summary>Reads one disk's facts, or <c>null</c> when it reports no device number or no capacity —
    /// an empty card reader and a device that has gone away look the same here, and neither deserves a
    /// card.</summary>
    private static BlockDeviceFacts? ReadDisk(IProcFileSystem proc, string name) {
        var root = BlockRoot + "/" + name;

        if (ParseDeviceNumber(proc.ReadAllText(root + "/dev")) is not { } number)
            return null;

        var sectors = ParseUInt64(proc.ReadAllText(root + "/size"));
        if (sectors == 0)
            return null;

        var rotational = proc.ReadAllText(root + "/queue/rotational")?.Trim() == "1";

        return new BlockDeviceFacts(
            name,
            number,
            sectors * SectorBytes,
            ModelOf(proc, root),
            DriveKinds.FromSysBlock(name, rotational));
    }

    /// <summary>The drive's model, falling back to its vendor — a SATA disk fills both ("VBOX HARDDISK" /
    /// "ATA"), a virtio disk fills neither. "" rather than a stand-in, so each consumer applies its
    /// own placeholder.</summary>
    private static string ModelOf(IProcFileSystem proc, string root) {
        var model = proc.ReadAllText(root + "/device/model")?.Trim() ?? "";
        if (model.Length > 0)
            return model;

        return proc.ReadAllText(root + "/device/vendor")?.Trim() ?? "";
    }

    /// <summary>A disk's partition directories, which the kernel nests inside the disk itself and prefixes
    /// with its name (<c>sda/sda1</c>, <c>nvme0n1/nvme0n1p1</c>) — the partition→disk map without resolving
    /// a single symlink. The disk's own sibling files (<c>size</c>, <c>queue</c>) do not carry the
    /// prefix.</summary>
    private static IEnumerable<string> Partitions(IProcFileSystem proc, string disk) {
        foreach (var entry in proc.ListDirectory(BlockRoot + "/" + disk))
            if (entry.Length > disk.Length && entry.StartsWith(disk, StringComparison.Ordinal))
                yield return entry;
    }

    /// <summary>The first device backing this one, or <c>null</c> when it has no <c>slaves</c> — which is
    /// what makes it a real disk. A RAID device has several; the first is enough to file its capacity
    /// against a drive that exists.</summary>
    private static string? SlaveOf(IProcFileSystem proc, string name) {
        foreach (var slave in proc.ListDirectory(BlockRoot + "/" + name + "/slaves"))
            return slave;

        return null;
    }

    /// <summary>Follows a mapper/RAID device down to the disk that backs it. Chains (LUKS over LVM over a
    /// partition) are followed to a cap; a chain that leaves the known devices yields <c>null</c> and the
    /// caller drops the mount.</summary>
    private static int? ResolveAlias(
        IProcFileSystem proc, string name, IReadOnlyDictionary<string, int> numberByDevice) {
        var current = name;

        for (var depth = 0; depth < MaxSlaveDepth; depth++) {
            if (SlaveOf(proc, current) is not { } slave)
                return null;

            if (numberByDevice.TryGetValue(slave, out var number))
                return number;

            current = slave;
        }

        return null;
    }

    /// <summary>Parses a <c>dev</c> file's <c>"8:0"</c> body into a packed disk number; <c>null</c> when
    /// absent or malformed.</summary>
    private static int? ParseDeviceNumber(string? text) {
        if (text is null)
            return null;

        var body = text.AsSpan().Trim();
        var colon = body.IndexOf(':');
        if (colon <= 0)
            return null;

        if (!int.TryParse(body[..colon], NumberStyles.None, CultureInfo.InvariantCulture, out var major)
            || !int.TryParse(body[(colon + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out var minor))
            return null;

        return Pack(major, minor);
    }

    private static ulong ParseUInt64(string? text) =>
        text is not null
        && ulong.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;
}
