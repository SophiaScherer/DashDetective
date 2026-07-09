using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace DashDetective.Tabs.FileExplorer;

/// <summary>A drive shown as a tree root, e.g. "Local Disk (C:)".</summary>
public readonly record struct DriveEntry(string DisplayName, string RootPath);

/// <summary>A subdirectory entry (name + full path).</summary>
public readonly record struct DirEntry(string Name, string FullPath);

/// <summary>
/// Async, soft-failing filesystem enumeration for the File Explorer. Mirrors the Dashboard
/// <c>*InfoProvider</c> pattern: work runs off the UI thread via <see cref="Task.Run"/>, guarded
/// by <see cref="OperatingSystem.IsWindows"/>, and every entry is read defensively so a protected
/// or disappearing folder yields a partial list instead of throwing.
/// </summary>
public static class DirectoryService {
    // Hide OS hidden/system entries (as Explorer does by default) and never throw on an
    // inaccessible child mid-enumeration.
    private static readonly EnumerationOptions EnumOpts = new() {
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
    };

    public static Task<IReadOnlyList<DriveEntry>> GetDrivesAsync() => Task.Run(ReadDrives);

    public static Task<IReadOnlyList<DirEntry>> GetSubdirectoriesAsync(string path) =>
        Task.Run(() => ReadSubdirectories(path));

    private static IReadOnlyList<DriveEntry> ReadDrives() {
        var drives = new List<DriveEntry>();
        if (!OperatingSystem.IsWindows())
            return drives;
        try {
            foreach (var d in DriveInfo.GetDrives()) {
                try {
                    if (!d.IsReady)
                        continue;
                    var label = string.IsNullOrWhiteSpace(d.VolumeLabel)
                        ? DriveTypeLabel(d.DriveType)
                        : d.VolumeLabel;
                    var letter = d.Name.TrimEnd(Path.DirectorySeparatorChar);
                    drives.Add(new DriveEntry($"{label} ({letter})", d.RootDirectory.FullName));
                } catch {
                    // Skip a drive that can't be described (e.g. removed mid-scan).
                }
            }
        } catch {
            // Return whatever we managed to collect.
        }
        return drives;
    }

    private static IReadOnlyList<DirEntry> ReadSubdirectories(string path) {
        var dirs = new List<DirEntry>();
        try {
            foreach (var sub in new DirectoryInfo(path).EnumerateDirectories("*", EnumOpts)) {
                try {
                    dirs.Add(new DirEntry(sub.Name, sub.FullName));
                } catch {
                    // Skip an entry we can't read.
                }
            }
        } catch {
            // Unauthorized / path gone: return whatever we have (possibly empty).
        }
        dirs.Sort(static (a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return dirs;
    }

    private static string DriveTypeLabel(DriveType type) => type switch {
        DriveType.Fixed => "Local Disk",
        DriveType.Removable => "Removable Disk",
        DriveType.Network => "Network Drive",
        DriveType.CDRom => "CD Drive",
        DriveType.Ram => "RAM Disk",
        _ => "Disk",
    };
}
