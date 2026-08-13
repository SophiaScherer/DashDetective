using DashDetective.Services.Platform.Linux;
using DashDetective.Tabs.FileExplorer;
using DashDetective.Tests.Fakes;
using System;
using System.Linq;
using Xunit;

namespace DashDetective.Tests.Tabs.FileExplorer;

/// <summary>
/// Covers the <see cref="IFileSystemRoots"/> seam: which set the platform resolves to, and the Linux
/// rule for which of a machine's several dozen mounts are worth showing as tree roots.
///
/// The mount rule is pure, so every assertion about it runs on a Windows dev machine too — it is
/// <see cref="ProcFixtures.ProcMounts"/> text in and mount points out, and nothing about it needs a
/// Linux host to be true.
/// </summary>
public class FileSystemRootsTests {
    private static MountEntry[] Mounts(string text) =>
        [.. ProcMountsParser.Parse(text.Split('\n'))];

    [Fact]
    public void ForCurrentPlatform_ResolvesTheRootsForThisHost() {
        var roots = IFileSystemRoots.ForCurrentPlatform();

        if (OperatingSystem.IsWindows())
            Assert.IsType<WindowsFileSystemRoots>(roots);
        else if (OperatingSystem.IsLinux())
            Assert.IsType<LinuxFileSystemRoots>(roots);
        else
            Assert.IsType<UnsupportedFileSystemRoots>(roots);
    }

    /// <summary>The behaviour the old <c>OperatingSystem.IsWindows()</c> guard in
    /// <c>DirectoryService.ReadDrives</c> produced off Windows, kept for any third platform.</summary>
    [Fact]
    public void Unsupported_OffersNoRoots() =>
        Assert.Empty(new UnsupportedFileSystemRoots().Read());

    /// <summary>
    /// The central claim of the rule. A real desktop's <c>/proc/mounts</c> is dominated by pseudo
    /// filesystems and one squashfs per installed snap; listing them would bury the entries anyone
    /// wants. Only the removable mount is a root here — <c>/</c> and home are added separately, not
    /// derived from the mount table.
    /// </summary>
    [Fact]
    public void RemovableMountPoints_KeepsOnlyDesktopMediaMounts() {
        var points = LinuxFileSystemRoots.RemovableMountPoints(Mounts(ProcFixtures.ProcMounts));

        Assert.Equal(["/media/user/My Backup"], points);
    }

    /// <summary>The mount point is octal-escaped in the file (<c>\040</c> for a space), and a root
    /// carrying a visible "\040" is one the tree cannot open. The unescape is the parser's, so this
    /// pins that the rule reads through it rather than around it.</summary>
    [Fact]
    public void RemovableMountPoints_UnescapesTheMountPoint() {
        var points = LinuxFileSystemRoots.RemovableMountPoints(Mounts(ProcFixtures.ProcMounts));

        Assert.DoesNotContain(points, p => p.Contains('\\', StringComparison.Ordinal));
    }

    /// <summary>udisks2 mounts under <c>/media/$USER</c> on Debian and Ubuntu but <c>/run/media/$USER</c>
    /// on Fedora and Arch. Matching only the first would leave half the distros with no removable roots.
    /// <c>/mnt</c> is the hand-mounted case.</summary>
    [Theory]
    [InlineData("/media/sophia/USB")]
    [InlineData("/run/media/sophia/USB")]
    [InlineData("/mnt/data")]
    public void RemovableMountPoints_AcceptsEveryDesktopMediaPrefix(string mountPoint) {
        var points = LinuxFileSystemRoots.RemovableMountPoints(
            Mounts($"/dev/sdb1 {mountPoint} exfat rw 0 0"));

        Assert.Equal([mountPoint], points);
    }

    /// <summary>The prefix must have something after it: <c>/media</c> and <c>/mnt</c> themselves are the
    /// empty parent directories every distro ships, not volumes.</summary>
    [Theory]
    [InlineData("/media")]
    [InlineData("/mnt")]
    [InlineData("/run/media")]
    [InlineData("/")]
    [InlineData("/home/sophia")]
    public void RemovableMountPoints_RejectsAParentOrAnOrdinaryPath(string mountPoint) =>
        Assert.Empty(LinuxFileSystemRoots.RemovableMountPoints(
            Mounts($"/dev/sdb1 {mountPoint} ext4 rw 0 0")));

    /// <summary>A device can be listed against one mount point more than once (bind mounts), and a
    /// duplicated root would draw the same branch twice.</summary>
    [Fact]
    public void RemovableMountPoints_DedupesARepeatedMountPoint() {
        var points = LinuxFileSystemRoots.RemovableMountPoints(Mounts(
            """
            /dev/sdb1 /mnt/data ext4 rw 0 0
            /dev/sdb1 /mnt/data ext4 rw 0 0
            """));

        Assert.Single(points);
    }

    /// <summary>Under <c>/media/$USER</c> the last segment is the label udisks named the mount after —
    /// the direct analogue of a Windows <c>VolumeLabel</c>, and what the tree row is titled with.</summary>
    [Theory]
    [InlineData("/media/user/My Backup", "My Backup")]
    [InlineData("/mnt/data", "data")]
    [InlineData("/", "/")]
    public void LeafName_TakesTheLabelTheMountIsNamedAfter(string mountPoint, string expected) =>
        Assert.Equal(expected, LinuxFileSystemRoots.LeafName(mountPoint));

    /// <summary>The reader is portable managed code over the <c>/proc</c> seam, so it constructs
    /// anywhere; off Linux the mount table simply isn't there. The root and home rows still appear,
    /// because those come from the environment rather than the file — so this asserts what a real
    /// <c>/proc/mounts</c> read cannot add, not that the list is empty.</summary>
    [Fact]
    public void LinuxReader_ConstructsAndDegradesOnAnyHost() {
        var roots = new LinuxFileSystemRoots(new FakeProcFileSystem()).Read();

        Assert.DoesNotContain(roots, r => r.RootPath.StartsWith("/media/", StringComparison.Ordinal));
    }

    /// <summary>The two rows that are always offered, whatever the mount table says. Home is skipped
    /// only when it resolves to the root that is already listed, which is what a broken passwd entry
    /// looks like.</summary>
    [Fact]
    public void LinuxReader_AlwaysOffersTheFilesystemRoot() {
        var roots = new LinuxFileSystemRoots(
            new FakeProcFileSystem().WithFile("/proc/mounts", ProcFixtures.ProcMounts)).Read();

        Assert.Contains(roots, r => r.RootPath == "/");
        Assert.Contains(roots, r => r.RootPath == "/media/user/My Backup");
        Assert.Equal(roots.Select(r => r.RootPath).Distinct().Count(), roots.Count);
    }
}
