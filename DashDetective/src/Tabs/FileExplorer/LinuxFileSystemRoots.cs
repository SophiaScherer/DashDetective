using DashDetective.Services.Platform.Linux;
using DashDetective.Shared;
using System;
using System.Collections.Generic;

namespace DashDetective.Tabs.FileExplorer;

/// <summary>
/// The places a Linux desktop user starts from: the filesystem root, home, and any removable media.
/// There is no drive-letter list to enumerate, so the third group comes from <c>/proc/mounts</c> — but
/// a machine has dozens of mounts (cgroup, tmpfs, one squashfs per snap), and listing them would bury
/// the two entries anyone wants. See <see cref="RemovableMountPoints"/> for what survives.
/// </summary>
internal sealed class LinuxFileSystemRoots : IFileSystemRoots {
    // Concatenated forward-slash literal, never Path.Combine — see IProcFileSystem.
    private const string MountsPath = "/proc/mounts";

    // The prefixes a desktop mounts removable media under. udisks2 uses /media/$USER on Debian and
    // Ubuntu and /run/media/$USER on Fedora and Arch; /mnt is where a person mounts things by hand.
    // Matched as prefixes rather than as "/media/$USER/*" so no user name has to be resolved, and so a
    // volume mounted by another mechanism under the same tree is still found.
    private static readonly string[] RemovablePrefixes = ["/media/", "/run/media/", "/mnt/"];

    private readonly IProcFileSystem _proc;

    public LinuxFileSystemRoots() : this(new ProcFileSystem()) { }

    /// <summary>Test seam: injects the filesystem so the mount read runs against canned fixtures from
    /// any dev machine.</summary>
    internal LinuxFileSystemRoots(IProcFileSystem proc) => _proc = proc;

    public IReadOnlyList<DriveEntry> Read() {
        var roots = new List<DriveEntry>();
        var seen = new HashSet<string>(PathComparison.Comparer);

        Add(roots, seen, "Filesystem", "/");

        // Empty when HOME is unset (a service context) — then there is simply no home row.
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(home))
            Add(roots, seen, "Home", home);

        foreach (var mount in RemovableMountPoints(ProcMountsParser.Parse(_proc.ReadAllLines(MountsPath))))
            Add(roots, seen, LeafName(mount), mount);

        return roots;
    }

    /// <summary>
    /// The mount points that count as removable media, deduplicated and in a stable order. Pure, so the
    /// rule is testable against a canned <c>/proc/mounts</c>. The prefix match requires a trailing
    /// segment, so <c>/mnt</c> itself is not offered — that is the empty parent, not a volume.
    /// </summary>
    internal static IReadOnlyList<string> RemovableMountPoints(IReadOnlyList<MountEntry> mounts) {
        var points = new List<string>();
        var seen = new HashSet<string>(PathComparison.Comparer);

        foreach (var mount in mounts) {
            if (!IsRemovable(mount.MountPoint) || !seen.Add(mount.MountPoint))
                continue;

            points.Add(mount.MountPoint);
        }

        // Presentation ordering, so it stays OrdinalIgnoreCase on every platform.
        points.Sort(static (a, b) => string.Compare(a, b, StringComparison.OrdinalIgnoreCase));
        return points;
    }

    private static bool IsRemovable(string mountPoint) {
        foreach (var prefix in RemovablePrefixes)
            if (mountPoint.StartsWith(prefix, PathComparison.Comparison) && mountPoint.Length > prefix.Length)
                return true;

        return false;
    }

    /// <summary>The last path segment. Under <c>/media/$USER</c> that is the volume label udisks named
    /// the mount after — the direct analogue of a Windows <c>VolumeLabel</c>.</summary>
    internal static string LeafName(string mountPoint) {
        var cut = mountPoint.LastIndexOf('/');
        return cut >= 0 && cut < mountPoint.Length - 1 ? mountPoint[(cut + 1)..] : mountPoint;
    }

    // Skips a path already listed, so the tree never draws the same branch twice — home is "/" on a
    // broken passwd entry, and a mount point can coincide with either.
    private static void Add(List<DriveEntry> roots, HashSet<string> seen, string label, string path) {
        if (!seen.Add(path))
            return;

        roots.Add(new DriveEntry($"{label} ({path})", path, DirectoryService.RootHasChildren(path)));
    }
}
