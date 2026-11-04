using DashDetective.Shared.Shortcuts;
using DashDetective.Tabs.FileExplorer;
using DashDetective.Tests.Fakes;
using System;
using System.IO;
using Xunit;

namespace DashDetective.Tests.Tabs.FileExplorer;

/// <summary>
/// Covers <see cref="FileExplorerViewModel"/>'s side of the back precedence: its folder back claims Alt+←
/// and the mouse back button while there is a folder to return to, and reports the key unhandled at the
/// start of its trail, which is what lets the shell fall through to page Back.
/// </summary>
public sealed class FileExplorerViewModelTests : IDisposable {
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "DashDetectiveTests_" + Path.GetRandomFileName());

    private readonly FileExplorerViewModel _page = new(new FakeShellInterop(), new UnsupportedFileSystemRoots());

    public FileExplorerViewModelTests() => Directory.CreateDirectory(_root);

    public void Dispose() {
        _page.Dispose();
        try {
            Directory.Delete(_root, recursive: true);
        } catch (IOException) {
            // A watcher or a pending load may still hold the folder; the temp directory is fine to leave.
        }
    }

    [Fact]
    public void HandleShortcut_NavigateBackWithNoFolderHistory_IsUnhandled() =>
        Assert.False(_page.HandleShortcut(ShortcutId.NavigateBack));

    [Fact]
    public void HandleShortcut_NavigateBackAtTheFirstFolderOpened_IsUnhandled() {
        _page.Reveal(_root);

        Assert.False(_page.HandleShortcut(ShortcutId.NavigateBack));
    }

    [Fact]
    public void HandleShortcut_NavigateBackWithFolderHistory_StepsBackAFolder() {
        var child = Directory.CreateDirectory(Path.Combine(_root, "child")).FullName;
        _page.Reveal(_root);
        _page.Reveal(child);

        Assert.True(_page.HandleShortcut(ShortcutId.NavigateBack));
        Assert.Equal(Path.GetFullPath(_root), _page.CurrentPath);
        Assert.False(_page.HandleShortcut(ShortcutId.NavigateBack));
    }

    /// <summary>Page Back is the shell's alone; the tab never claims it, even with folder history.</summary>
    [Fact]
    public void HandleShortcut_PageBack_IsNeverClaimed() {
        var child = Directory.CreateDirectory(Path.Combine(_root, "child")).FullName;
        _page.Reveal(_root);
        _page.Reveal(child);

        Assert.False(_page.HandleShortcut(ShortcutId.Back));
    }

    [Theory]
    [InlineData("C:", @"C:\")]
    [InlineData("d:", @"d:\")]
    [InlineData(@"C:\Users", @"C:\Users")]
    [InlineData("C:/Users", "C:/Users")]
    [InlineData("C:foo", null)]
    [InlineData("Users", null)]
    [InlineData(@"sub\dir", null)]
    public void RootTypedPath_Windows_RootsADriveLetterAndRefusesRelative(string typed, string? expected) {
        if (!OperatingSystem.IsWindows()) {
            return; // Path.IsPathRooted treats a backslash path as relative off Windows.
        }

        Assert.Equal(expected, FileExplorerViewModel.RootTypedPath(typed, windows: true));
    }

    [Theory]
    [InlineData("C:")]
    [InlineData("Users")]
    public void RootTypedPath_NotWindows_RefusesRelative(string typed) {
        if (OperatingSystem.IsWindows()) {
            return; // "Users" is relative on every host, but "C:" is rooted on Windows; assert on Unix hosts.
        }

        Assert.Null(FileExplorerViewModel.RootTypedPath(typed, windows: false));
    }

    [Fact]
    public void CommitPath_BareDriveLetter_OpensTheDriveRoot() {
        if (!OperatingSystem.IsWindows()) {
            return; // A bare drive letter is only a drive on Windows.
        }

        var drive = Path.GetPathRoot(_root)!;
        _page.PathText = drive.TrimEnd(Path.DirectorySeparatorChar);

        _page.CommitPathCommand.Execute(null);

        Assert.Equal(drive, _page.CurrentPath);
    }

    /// <summary>The folder exists relative to the working directory, which is exactly what a bypassed
    /// check would resolve it against, so only the refusal keeps it from opening.</summary>
    [Fact]
    public void CommitPath_RelativeNameThatExistsInTheWorkingDirectory_IsRefusedWithThePathError() {
        var name = "DashDetectiveRel_" + Path.GetRandomFileName();
        var folder = Directory.CreateDirectory(Path.Combine(Environment.CurrentDirectory, name)).FullName;
        try {
            var before = _page.CurrentPath;
            _page.PathText = name;

            _page.CommitPathCommand.Execute(null);

            Assert.Equal(before, _page.CurrentPath);
            Assert.Equal($"Can't find \"{name}\"", _page.PathError);
        } finally {
            Directory.Delete(folder);
        }
    }
}
