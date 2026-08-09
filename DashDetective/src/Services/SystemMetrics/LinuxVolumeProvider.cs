using DashDetective.Services.Diagnostics;
using DashDetective.Services.Platform.Linux;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DashDetective.Services.SystemMetrics;

/// <summary>
/// Enumerates the machine's mounted volumes from <c>/proc/mounts</c>, sized through
/// <see cref="IVolumeCapacityReader"/> and joined to their host disk through the shared
/// <see cref="SysBlockFacts"/> derivation.
///
/// <b>A mount is kept only when its device resolves to a disk that has a card.</b> That one rule does the
/// whole job of filtering: <c>tmpfs</c>, <c>cgroup</c>, <c>proc</c> and friends name no <c>/dev</c> device,
/// and every snap mount resolves to a <c>loop</c> device that <see cref="SysBlockFacts"/> has already
/// excluded — so the ~30 pseudo-filesystems and ~25 loop mounts of a stock Ubuntu GNOME install all fall
/// out, and no volume is left pointing at a disk with no card. A filesystem-type allowlist would be a
/// weaker second guess at the same question.
///
/// <b>Repeated devices are collapsed to one volume.</b> <c>/proc/mounts</c> lists the same device many
/// times — bind mounts, <c>/var/snap</c>, btrfs subvolumes — and the drive cards <i>sum</i> their volumes'
/// sizes, so leaving duplicates in multiplies a drive's capacity and used space several-fold. The shortest
/// mount point wins, which is the one a user thinks of as the volume.
///
/// Stateless and never throws: any failure yields an empty list.
/// </summary>
internal sealed class LinuxVolumeProvider : IVolumeProvider {
    // Concatenated forward-slash literals, never Path.Combine — see IProcFileSystem.
    private const string MountsPath = "/proc/mounts";
    private const string DevRoot = "/dev/";
    private const string ByLabelRoot = "/dev/disk/by-label";

    private readonly IProcFileSystem _proc;
    private readonly IVolumeCapacityReader _capacity;

    public LinuxVolumeProvider() : this(new ProcFileSystem(), new VolumeCapacityReader()) { }

    /// <summary>Test seam: injects the filesystem and the capacity reader so the mount filter, the dedupe
    /// and the disk join can be exercised against canned fixtures from any dev machine.</summary>
    internal LinuxVolumeProvider(IProcFileSystem proc, IVolumeCapacityReader capacity) {
        _proc = proc;
        _capacity = capacity;
    }

    public Task<IReadOnlyList<VolumeInfo>> GetAsync() => Task.Run(Read);

    private IReadOnlyList<VolumeInfo> Read() {
        try {
            var blocks = SysBlockFacts.Read(_proc);
            var labels = ReadLabels();

            // Keyed by resolved kernel device name, so /dev/mapper/root and /dev/dm-0 collapse together.
            var byDevice = new Dictionary<string, VolumeInfo>(StringComparer.Ordinal);

            foreach (var mount in ProcMountsParser.Parse(_proc.ReadAllLines(MountsPath))) {
                if (DeviceNameOf(mount.Device) is not { } device
                    || blocks.DiskNumberFor(device) is not { } diskNumber)
                    continue;

                if (byDevice.TryGetValue(device, out var existing)
                    && existing.MountPoint.Length <= mount.MountPoint.Length)
                    continue;

                var capacity = _capacity.Read(mount.MountPoint);
                if (capacity.SizeBytes == 0)
                    continue;

                byDevice[device] = new VolumeInfo(
                    diskNumber,
                    DriveLetter: null,
                    labels.TryGetValue(device, out var label) ? label : "",
                    mount.FileSystem,
                    capacity.SizeBytes,
                    capacity.FreeBytes,
                    GptType: "",
                    mount.MountPoint);
            }

            return [.. byDevice.Values];
        } catch (Exception e) {
            Log.Warn("LinuxVolumeProvider read failed", e);
            return Array.Empty<VolumeInfo>();
        }
    }

    /// <summary>
    /// The kernel device name behind a mount's device field — <c>/dev/sda2</c> → <c>sda2</c>. Names that
    /// are not a direct <c>/dev</c> entry (<c>/dev/mapper/…</c>, <c>/dev/disk/by-uuid/…</c>) are symlinks,
    /// so they are resolved to the real node. <c>null</c> for anything that is not a device at all
    /// (<c>tmpfs</c>, <c>cgroup2</c>, <c>systemd-1</c>), which is what drops the pseudo-filesystems.
    /// </summary>
    private string? DeviceNameOf(string device) {
        if (!device.StartsWith(DevRoot, StringComparison.Ordinal))
            return null;

        var name = device[DevRoot.Length..];
        if (!name.Contains('/', StringComparison.Ordinal))
            return name;

        return _proc.ResolveLink(device) is { } target ? NameAfterLastSlash(target) : null;
    }

    /// <summary>Reads the filesystem labels udev publishes as symlinks under
    /// <c>/dev/disk/by-label</c>, keyed by the device they point at. Absent on a machine with no labelled
    /// filesystems, which simply yields no labels.</summary>
    private Dictionary<string, string> ReadLabels() {
        var labels = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var entry in _proc.ListDirectory(ByLabelRoot)) {
            if (_proc.ResolveLink(ByLabelRoot + "/" + entry) is not { } target)
                continue;

            labels[NameAfterLastSlash(target)] = ProcMountsParser.UnescapeUdev(entry);
        }

        return labels;
    }

    private static string NameAfterLastSlash(string path) {
        var slash = path.LastIndexOf('/');
        return slash >= 0 ? path[(slash + 1)..] : path;
    }
}
