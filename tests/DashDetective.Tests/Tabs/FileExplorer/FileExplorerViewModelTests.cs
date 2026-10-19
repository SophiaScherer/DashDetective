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
}
