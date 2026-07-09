using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

namespace DashDetective.Tabs.FileExplorer;

/// <summary>A drive shown as a tree root, e.g. "Local Disk (C:)".</summary>
public readonly record struct DriveEntry(string DisplayName, string RootPath);

/// <summary>A subdirectory entry (name + full path).</summary>
public readonly record struct DirEntry(string Name, string FullPath);

/// <summary>
/// A file-list entry with its display strings already computed off the UI thread (type name,
/// modified date, humanised size). <see cref="FileEntry"/> wraps this with the themed glyph and
/// selection behaviour. <paramref name="Size"/> (bytes; -1 for folders) and <paramref name="Modified"/>
/// are the raw values the column sorting compares against — the display strings can't be ordered.
/// </summary>
public readonly record struct FileItem(
    string Name, string FullPath, bool IsDirectory,
    string TypeName, string ModifiedText, string SizeText, string Extension,
    string CreatedText, string AttributesText,
    long Size, DateTime Modified);

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

    public static Task<IReadOnlyList<FileItem>> GetEntriesAsync(string path) =>
        Task.Run(() => ReadEntries(path));

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

    // Folders first (both alphabetical), matching Explorer's default ordering. Each entry's
    // type name, date and size are computed here, off the UI thread.
    private static IReadOnlyList<FileItem> ReadEntries(string path) {
        var dirs = new List<FileItem>();
        var files = new List<FileItem>();
        try {
            var di = new DirectoryInfo(path);
            foreach (var sub in di.EnumerateDirectories("*", EnumOpts)) {
                try {
                    dirs.Add(new FileItem(sub.Name, sub.FullName, true,
                        ShellInterop.GetTypeName(sub.FullName, true),
                        FormatDate(sub.LastWriteTime), "—", "",
                        FormatDate(sub.CreationTime), FormatAttributes(sub.Attributes),
                        -1, sub.LastWriteTime));
                } catch {
                    // Skip an entry we can't read.
                }
            }
            foreach (var f in di.EnumerateFiles("*", EnumOpts)) {
                try {
                    files.Add(new FileItem(f.Name, f.FullName, false,
                        ShellInterop.GetTypeName(f.FullName, false),
                        FormatDate(f.LastWriteTime), FileSizeFormatter.Format(f.Length),
                        f.Extension,
                        FormatDate(f.CreationTime), FormatAttributes(f.Attributes),
                        f.Length, f.LastWriteTime));
                } catch {
                    // Skip an entry we can't read.
                }
            }
        } catch {
            // Unauthorized / path gone: return whatever we have.
        }

        dirs.Sort(NameCompare);
        files.Sort(NameCompare);
        var all = new List<FileItem>(dirs.Count + files.Count);
        all.AddRange(dirs);
        all.AddRange(files);
        return all;
    }

    private static int NameCompare(FileItem a, FileItem b) =>
        string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);

    private static string FormatDate(DateTime dt) =>
        dt.ToString("MMM dd, yyyy", CultureInfo.InvariantCulture);

    // Terse flag letters, e.g. "A", "RA". Hidden/System entries are already filtered out.
    private static string FormatAttributes(FileAttributes attributes) {
        var flags = new System.Text.StringBuilder();
        if ((attributes & FileAttributes.ReadOnly) != 0) flags.Append('R');
        if ((attributes & FileAttributes.Hidden) != 0) flags.Append('H');
        if ((attributes & FileAttributes.System) != 0) flags.Append('S');
        if ((attributes & FileAttributes.Archive) != 0) flags.Append('A');
        return flags.Length == 0 ? "—" : flags.ToString();
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
