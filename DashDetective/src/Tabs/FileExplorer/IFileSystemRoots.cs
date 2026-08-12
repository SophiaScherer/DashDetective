using System;
using System.Collections.Generic;

namespace DashDetective.Tabs.FileExplorer;

/// <summary>
/// Where the File Explorer tree starts — the drives on Windows, the mounted places a desktop user
/// recognises on Linux. Implementations must never throw: an unreadable root is skipped, so a partial
/// list is returned rather than none.
/// </summary>
internal interface IFileSystemRoots {
    /// <summary>The tree's top-level entries, in display order. Empty where the platform offers none.</summary>
    IReadOnlyList<DriveEntry> Read();

    /// <summary>The roots for this machine, or an empty set where there is no rule for finding them.
    /// The platform is decided here and nowhere else.</summary>
    static IFileSystemRoots ForCurrentPlatform() =>
        OperatingSystem.IsWindows() ? new WindowsFileSystemRoots()
        : OperatingSystem.IsLinux() ? new LinuxFileSystemRoots()
        : new UnsupportedFileSystemRoots();
}
