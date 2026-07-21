using DashDetective.Services.Diagnostics;
using System;
using System.Collections.Generic;
using System.Management;
using System.Threading.Tasks;

namespace DashDetective.Services.SystemMetrics;

/// <summary>
/// One mounted volume: its host physical-disk number (for the drive-card rollup, <c>null</c> when it can't
/// be resolved), drive letter (<c>null</c> for unlettered partitions like Recovery/EFI), label, file
/// system, and total/free bytes. Sizes are raw so callers format them with <c>FileSizeFormatter</c>.
/// </summary>
public readonly record struct VolumeInfo(
    int? DiskNumber, char? DriveLetter, string Label, string FileSystem, ulong SizeBytes, ulong FreeBytes);

/// <summary>
/// Enumerates all mounted volumes from WMI <c>MSFT_Volume</c> (<c>root\Microsoft\Windows\Storage</c>) —
/// including the unlettered Recovery/EFI partitions the design comp shows, which <c>System.IO.DriveInfo</c>
/// would omit. Each volume is joined to its host disk via <c>MSFT_Partition</c> (matching the volume's
/// device path against the partition's access paths), so the drive-card rollup can sum used space per disk.
/// The query is comparatively slow and blocking, so it runs on a background thread; any failure (or a
/// non-Windows host) yields an empty list rather than throwing. Mirrors <see cref="DiskInfoProvider"/>.
/// </summary>
public static class VolumeProvider {
    public static Task<IReadOnlyList<VolumeInfo>> GetAsync() => Task.Run(Read);

    private static IReadOnlyList<VolumeInfo> Read() {
        // Guard doubles as the platform-compatibility check for the WMI calls below.
        if (!OperatingSystem.IsWindows())
            return Array.Empty<VolumeInfo>();

        try {
            var scope = new ManagementScope(@"\\.\root\Microsoft\Windows\Storage");
            scope.Connect();

            // Access path (e.g. "C:\" or "\\?\Volume{guid}\") → host disk number, so both lettered and
            // unlettered volumes can be traced back to a physical disk.
            var diskByAccessPath = BuildAccessPathToDiskMap(scope);

            var volumes = new List<VolumeInfo>();
            var query = new ObjectQuery(
                "SELECT DriveLetter, FileSystemLabel, FileSystem, Size, SizeRemaining, Path FROM MSFT_Volume");
            using var searcher = new ManagementObjectSearcher(scope, query);
            using var results = searcher.Get();

            foreach (var obj in results) {
                using (obj) {
                    // Skip volumes with no media/capacity (e.g. an empty optical drive).
                    var size = ToUInt64(obj["Size"]);
                    if (size == 0)
                        continue;

                    var path = obj["Path"] as string;
                    int? disk = path is not null && diskByAccessPath.TryGetValue(path, out var number)
                        ? number
                        : null;

                    volumes.Add(new VolumeInfo(
                        disk,
                        DriveLetterOrNull(obj["DriveLetter"]),
                        (obj["FileSystemLabel"] as string ?? "").Trim(),
                        (obj["FileSystem"] as string ?? "").Trim(),
                        size,
                        ToUInt64(obj["SizeRemaining"])));
                }
            }

            return volumes;
        } catch (Exception e) {
            Log.Warn("VolumeProvider read failed", e);
            return Array.Empty<VolumeInfo>();
        }
    }

    /// <summary>Maps every partition access path to its disk number. A volume's <c>Path</c> appears among its
    /// partition's <c>AccessPaths</c>, so this keys the volume→disk join. Missing/failed → empty (volumes
    /// then simply carry a null disk number).</summary>
    private static Dictionary<string, int> BuildAccessPathToDiskMap(ManagementScope scope) {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        try {
            var query = new ObjectQuery("SELECT DiskNumber, AccessPaths FROM MSFT_Partition");
            using var searcher = new ManagementObjectSearcher(scope, query);
            using var results = searcher.Get();

            foreach (var obj in results) {
                using (obj) {
                    var disk = ToInt(obj["DiskNumber"]);
                    if (obj["AccessPaths"] is string[] paths)
                        foreach (var path in paths)
                            if (!string.IsNullOrEmpty(path))
                                map[path] = disk;
                }
            }
        } catch {
            // Partition class unavailable — the volume→disk join is simply skipped.
        }
        return map;
    }

    /// <summary>Reads a CIM <c>char16</c> drive letter (returned by WMI as a char, numeric code or string)
    /// as an upper-case letter, or <c>null</c> when the volume has no drive letter.</summary>
    private static char? DriveLetterOrNull(object? value) {
        if (value is null)
            return null;

        var c = value switch {
            char ch => ch,
            ushort code => (char)code,
            string s when s.Length > 0 => s[0],
            _ => '\0',
        };
        return char.IsLetter(c) ? char.ToUpperInvariant(c) : null;
    }

    private static int ToInt(object? value) {
        try {
            return value is null ? 0 : Convert.ToInt32(value);
        } catch {
            return 0;
        }
    }

    private static ulong ToUInt64(object? value) {
        try {
            return value is null ? 0 : Convert.ToUInt64(value);
        } catch {
            return 0;
        }
    }
}
