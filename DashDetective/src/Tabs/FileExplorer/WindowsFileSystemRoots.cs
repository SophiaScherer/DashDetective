using System;
using System.Collections.Generic;
using System.IO;

namespace DashDetective.Tabs.FileExplorer;

/// <summary>
/// The ready drives, labelled as Explorer labels them — "Local Disk (C:)", "Removable Disk (E:)".
///
/// Carries no <c>[SupportedOSPlatform]</c>: <c>DriveInfo</c>, the <c>VolumeLabel</c> getter and
/// <see cref="DriveType"/> are all unannotated portable API, so the attribute would be decorative — the
/// <c>WindowsToolkitCatalog</c> case in AGENTS.md. The name says who authored the labels.
/// </summary>
internal sealed class WindowsFileSystemRoots : IFileSystemRoots {
    public IReadOnlyList<DriveEntry> Read() {
        var drives = new List<DriveEntry>();
        try {
            foreach (var d in DriveInfo.GetDrives()) {
                try {
                    if (!d.IsReady)
                        continue;
                    var label = string.IsNullOrWhiteSpace(d.VolumeLabel)
                        ? DriveTypeLabel(d.DriveType)
                        : d.VolumeLabel;
                    var letter = d.Name.TrimEnd(Path.DirectorySeparatorChar);
                    // Probe with the default (hidden-excluded) view — a ready drive effectively always
                    // has a visible subfolder, so the chevron shows as expected.
                    var root = d.RootDirectory.FullName;
                    drives.Add(new DriveEntry($"{label} ({letter})", root,
                                              DirectoryService.RootHasChildren(root)));
                } catch {
                    // Skip a drive that can't be described (e.g. removed mid-scan).
                }
            }
        } catch {
            // Return whatever we managed to collect.
        }
        return drives;
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

/// <summary>The no-rule set: no roots at all, which is byte-for-byte what the old
/// <c>OperatingSystem.IsWindows()</c> guard in <c>DirectoryService.ReadDrives</c> returned off
/// Windows. The tree simply stays empty, as it does today on macOS.</summary>
internal sealed class UnsupportedFileSystemRoots : IFileSystemRoots {
    public IReadOnlyList<DriveEntry> Read() => Array.Empty<DriveEntry>();
}
