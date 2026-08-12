using DashDetective.Tabs.FileExplorer;
using DashDetective.Tests.Fakes;
using System;
using Xunit;

namespace DashDetective.Tests.Tabs.FileExplorer;

/// <summary>
/// Covers <see cref="LinuxShellInterop"/> and its <see cref="FileTypeDescriptions"/> table: the friendly
/// names the File Explorer's Type column shows, and which folder the Properties button reveals.
///
/// Everything here is pure string work, so it all runs on a Windows dev machine. <c>Open</c> and
/// <c>ShowProperties</c>' launch are deliberately not exercised — they start a file manager, which is
/// not something a test suite should do to the machine running it.
/// </summary>
public class LinuxShellInteropTests {
    private static string TypeName(string path) => new LinuxShellInterop().GetTypeName(path, false);

    /// <summary>The desktop's own word, not Windows' "File folder".</summary>
    [Fact]
    public void GetTypeName_NamesADirectoryTheWayTheDesktopDoes() =>
        Assert.Equal("Folder", new LinuxShellInterop().GetTypeName("/home/sophia", true));

    /// <summary>A sample across the table's groups, in the desktop's "PNG image" wording rather than
    /// Windows' "PNG File" casing.</summary>
    [Theory]
    [InlineData("/home/s/notes.md", "Markdown document")]
    [InlineData("/home/s/photo.png", "PNG image")]
    [InlineData("/home/s/backup.tar", "Tar archive")]
    [InlineData("/home/s/song.flac", "FLAC audio")]
    [InlineData("/home/s/config.json", "JSON document")]
    [InlineData("/home/s/run.sh", "Shell script")]
    [InlineData("/home/s/app.desktop", "Desktop entry")]
    [InlineData("/home/s/nginx.service", "Systemd unit")]
    [InlineData("/usr/lib/libc.so", "Shared library")]
    public void GetTypeName_DescribesAKnownExtension(string path, string expected) =>
        Assert.Equal(expected, TypeName(path));

    /// <summary>Extensions are written in every case on Linux, and the map is keyed lowercase.</summary>
    [Theory]
    [InlineData("/home/s/PHOTO.PNG")]
    [InlineData("/home/s/Photo.Png")]
    public void GetTypeName_IsCaseInsensitive(string path) =>
        Assert.Equal("PNG image", TypeName(path));

    /// <summary>An unmapped extension still says something specific, in the same lowercase-"file"
    /// style as the mapped rows.</summary>
    [Fact]
    public void GetTypeName_FallsBackToTheExtensionForAnUnknownType() =>
        Assert.Equal("QZX file", TypeName("/home/s/thing.qzx"));

    /// <summary>
    /// The case a Windows-shaped reader gets wrong. A leading dot marks a hidden file on this platform,
    /// it is not an extension — so <c>.bashrc</c> must not be described as a "BASHRC file", and dotfiles
    /// are everywhere in a Linux home directory.
    /// </summary>
    [Theory]
    [InlineData("/home/s/.bashrc")]
    [InlineData("/home/s/.gitignore")]
    public void GetTypeName_TreatsALeadingDotAsHiddenRatherThanAnExtension(string path) =>
        Assert.Equal("Hidden file", TypeName(path));

    /// <summary>A hidden file with a real extension is still described by it.</summary>
    [Fact]
    public void GetTypeName_StillDescribesAHiddenFilesRealExtension() =>
        Assert.Equal("JSON document", TypeName("/home/s/.eslintrc.json"));

    /// <summary>Extensionless names are ordinary on Linux — README, Makefile, every binary in /usr/bin.</summary>
    [Theory]
    [InlineData("/home/s/README")]
    [InlineData("/usr/bin/ls")]
    [InlineData("/home/s/trailing.")]
    public void GetTypeName_ReportsAPlainFileWhenThereIsNoExtension(string path) =>
        Assert.Equal("File", TypeName(path));

    // ----- Properties -----

    /// <summary>Properties reveals the entry's containing folder, since no desktop offers a Properties
    /// dialog to a foreign process.
    ///
    /// Built through <see cref="TestPaths"/> because the subject calls <c>Path.GetDirectoryName</c>,
    /// which <b>normalises its result to the running host's separator</b> — a Linux-shaped literal
    /// asserts <c>\home\sophia</c> on a Windows dev box even though the reader is correct.</summary>
    [Fact]
    public void RevealTarget_IsTheContainingFolder() =>
        Assert.Equal(TestPaths.Of("home", "sophia"),
                     LinuxShellInterop.RevealTarget(TestPaths.Of("home", "sophia", "notes.md")));

    /// <summary>The filesystem root has no parent, so it reveals itself rather than nothing.</summary>
    [Fact]
    public void RevealTarget_OfARootlessPathIsItself() =>
        Assert.Equal(TestPaths.Root, LinuxShellInterop.RevealTarget(TestPaths.Root));

    /// <summary>Nothing selected, or a blank path, must not launch a file manager at all.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void RevealTarget_IsNullWhenThereIsNothingToReveal(string path) =>
        Assert.Null(LinuxShellInterop.RevealTarget(path));

    /// <summary>The code-behind calls this unconditionally on the selected row, so it must not throw
    /// whatever it is handed.</summary>
    [Fact]
    public void ShowProperties_DoesNotThrowOnAnEmptyPath() =>
        new LinuxShellInterop().ShowProperties(IntPtr.Zero, "");
}
