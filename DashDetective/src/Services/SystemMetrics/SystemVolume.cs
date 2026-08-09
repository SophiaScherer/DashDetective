using DashDetective.Shared;
using System.Collections.Generic;

namespace DashDetective.Services.SystemMetrics;

/// <summary>
/// Finds the volume the OS is installed on among a set of <see cref="VolumeInfo"/>, and through it the
/// physical disk hosting it. The Dashboard names that disk in its Storage tile and the Storage tab opens
/// its Disk Activity panel on it; both used to hold their own copy of the rule.
///
/// The two platforms identify the same volume differently, so the rule tries both: the drive letter from
/// <see cref="SystemDrive.Letter"/>, then the root mount point. A Windows volume carries no mount point and
/// a Linux one no letter, so only one arm can ever match.
/// </summary>
internal static class SystemVolume {
    private const string RootMountPoint = "/";

    /// <summary>The physical disk hosting the OS, or <c>null</c> when the system volume isn't present or
    /// can't be traced to a disk — callers then leave that surface at its last value rather than reporting
    /// another drive's.</summary>
    internal static int? FindDiskNumber(IReadOnlyList<VolumeInfo> volumes) {
        foreach (var volume in volumes)
            if (volume.DiskNumber is { } disk
                && (volume.DriveLetter == SystemDrive.Letter || volume.MountPoint == RootMountPoint))
                return disk;

        return null;
    }
}
