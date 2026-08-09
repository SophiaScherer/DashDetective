using System;
using System.IO;

namespace DashDetective.Services.Platform.Linux;

/// <summary>A mounted filesystem's total and free bytes, or zeroes when it can't be measured.</summary>
internal readonly record struct VolumeCapacity(ulong SizeBytes, ulong FreeBytes);

/// <summary>
/// Reads a mounted filesystem's capacity by mount point. <c>/proc/mounts</c> lists what is mounted but
/// carries no sizes, and the kernel's <c>statvfs</c> is reached through <c>DriveInfo</c> rather than a
/// pseudo-file — so this is a seam of its own beside <see cref="IProcFileSystem"/>, for the same reason:
/// without it <c>LinuxVolumeProvider</c> could not be tested until someone ran the VM.
///
/// Implementations must never throw: an unmounted or vanished path yields zeroes, which the caller drops.
/// </summary>
internal interface IVolumeCapacityReader {
    VolumeCapacity Read(string mountPoint);
}

/// <summary>
/// The production reader: <c>System.IO.DriveInfo</c>, which is the managed <c>statvfs</c> on Unix. Portable
/// managed code, so it carries no <c>[SupportedOSPlatform]</c>.
///
/// Free space is <c>TotalFreeSpace</c>, not <c>AvailableFreeSpace</c> — the drive cards subtract free from
/// total to get used, and only the former makes that subtraction agree with what <c>df</c> reports as Used.
/// It also matches what the Windows arm's <c>SizeRemaining</c> means.
/// </summary>
internal sealed class VolumeCapacityReader : IVolumeCapacityReader {
    public VolumeCapacity Read(string mountPoint) {
        try {
            var drive = new DriveInfo(mountPoint);
            if (!drive.IsReady || drive.TotalSize <= 0)
                return default;

            return new VolumeCapacity((ulong)drive.TotalSize, (ulong)Math.Max(drive.TotalFreeSpace, 0));
        } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) {
            return default;
        }
    }
}
