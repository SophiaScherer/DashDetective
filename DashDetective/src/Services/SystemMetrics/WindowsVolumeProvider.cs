using DashDetective.Services.Diagnostics;
using System;
using System.Collections.Generic;
using System.Management;
using System.Runtime.Versioning;
using System.Threading.Tasks;

namespace DashDetective.Services.SystemMetrics;


/// <summary>
/// Enumerates all mounted volumes from WMI <c>MSFT_Volume</c> (<c>root\Microsoft\Windows\Storage</c>) —
/// including the unlettered Recovery/EFI partitions the design comp shows, which <c>System.IO.DriveInfo</c>
/// would omit. Each volume is joined to its host disk via <c>MSFT_Partition</c> (matching the volume's
/// device path against the partition's access paths), so the drive-card rollup can sum used space per disk.
/// The query is comparatively slow and blocking, so it runs on a background thread; any failure (or a
/// non-Windows host) yields an empty list rather than throwing. Mirrors <see cref="PhysicalDiskProvider"/>.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsVolumeProvider : IVolumeProvider {
    public Task<IReadOnlyList<VolumeInfo>> GetAsync() => Task.Run(Read);

    private static IReadOnlyList<VolumeInfo> Read() {
        try {
            var scope = new ManagementScope(@"\\.\root\Microsoft\Windows\Storage");
            scope.Connect();

            // Access path (e.g. "C:\" or "\\?\Volume{guid}\") → host partition, so both lettered and
            // unlettered volumes can be traced back to a physical disk and their GPT type.
            var partitionByAccessPath = BuildAccessPathMap(scope);

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
                    PartitionRef? partition =
                        path is not null && partitionByAccessPath.TryGetValue(path, out var match)
                            ? match
                            : null;

                    volumes.Add(new VolumeInfo(
                        partition?.DiskNumber,
                        DriveLetterOrNull(obj["DriveLetter"]),
                        (obj["FileSystemLabel"] as string ?? "").Trim(),
                        (obj["FileSystem"] as string ?? "").Trim(),
                        size,
                        ToUInt64(obj["SizeRemaining"]),
                        partition?.GptType ?? ""));
                }
            }

            return volumes;
        } catch (Exception e) {
            Log.Warn("VolumeProvider read failed", e);
            return Array.Empty<VolumeInfo>();
        }
    }

    /// <summary>The partition facts a volume inherits: its host disk number (<c>null</c> when the machine
    /// doesn't report one) and raw GPT type GUID.</summary>
    private readonly record struct PartitionRef(int? DiskNumber, string GptType);

    /// <summary>Maps every partition access path to its partition. A volume's <c>Path</c> appears among its
    /// partition's <c>AccessPaths</c>, so this keys the volume→partition join. Missing/failed → empty
    /// (volumes then simply carry a null disk number and no GPT type).</summary>
    private static Dictionary<string, PartitionRef> BuildAccessPathMap(ManagementScope scope) {
        var map = new Dictionary<string, PartitionRef>(StringComparer.OrdinalIgnoreCase);
        try {
            var query = new ObjectQuery("SELECT DiskNumber, AccessPaths, GptType FROM MSFT_Partition");
            using var searcher = new ManagementObjectSearcher(scope, query);
            using var results = searcher.Get();

            foreach (var obj in results) {
                using (obj) {
                    // GptType is null on MBR disks — an empty string just means "no type to show".
                    var entry = new PartitionRef(ToDiskNumber(obj["DiskNumber"]), obj["GptType"] as string ?? "");
                    if (obj["AccessPaths"] is string[] paths)
                        foreach (var path in paths)
                            if (!string.IsNullOrEmpty(path))
                                map[path] = entry;
                }
            }
        } catch {
            // Partition class unavailable — the volume→partition join is simply skipped.
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

    /// <summary>Reads a partition's disk number, or <c>null</c> when the machine doesn't report one.
    /// Deliberately not 0-on-failure: 0 is a real disk number, so an unresolved partition would be filed
    /// onto the first drive and inflate its used/free rollup.</summary>
    private static int? ToDiskNumber(object? value) {
        if (value is null)
            return null;
        try {
            return Convert.ToInt32(value);
        } catch {
            return null;
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

/// <summary>The no-volumes set — what the old <c>OperatingSystem.IsWindows()</c> guard returned.</summary>
internal sealed class UnsupportedVolumeProvider : IVolumeProvider {
    public Task<IReadOnlyList<VolumeInfo>> GetAsync() => Task.FromResult<IReadOnlyList<VolumeInfo>>([]);
}
