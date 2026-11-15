using DashDetective.Tabs.FileExplorer;
using DashDetective.Tests.Fakes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace DashDetective.Tests.Tabs.FileExplorer;

/// <summary>Pins that syncing the tree's highlight to a navigation is not itself a navigation: the folder is not read a second time, which would drop a search jump's selection.</summary>
public sealed class FileExplorerTreeSyncTests : IDisposable {
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "DashDetectiveTests_" + Path.GetRandomFileName());

    private readonly string _first;
    private readonly string _second;
    private readonly string _file;
    private readonly FakeShellInterop _shell = new();
    private readonly FileExplorerViewModel _page;

    public FileExplorerTreeSyncTests() {
        _first = Directory.CreateDirectory(Path.Combine(_root, "first")).FullName;
        _second = Directory.CreateDirectory(Path.Combine(_root, "second")).FullName;
        _file = Path.Combine(_first, "a.txt");
        File.WriteAllText(_file, "");
        _page = new FileExplorerViewModel(_shell, new FixedRoots(_first, _second));
    }

    public void Dispose() {
        _page.Dispose();
        try {
            Directory.Delete(_root, recursive: true);
        } catch (IOException) {
            // A watcher or a pending load may still hold the folder; the temp directory is fine to leave.
        }
    }

    [Fact]
    public async Task Reveal_FileWhoseFolderHasALoadedTreeNode_ReadsTheFolderOnce() {
        await SettleAsync();

        _page.Reveal(_file);
        await SettleAsync();

        Assert.Single(_shell.TypeNameCalls, _file);
        Assert.Same(_page.RootNodes[0], _page.SelectedNode);
        Assert.True(_page.RootNodes[0].IsSelected);
    }

    [Fact]
    public async Task SelectingATreeNode_OpensItsFolderAndReadsItOnce() {
        await SettleAsync();

        _page.RootNodes[0].IsSelected = true;
        await SettleAsync();

        Assert.Equal(_first, _page.CurrentPath);
        Assert.Single(_shell.TypeNameCalls, _file);
    }

    [Fact]
    public async Task SelectingAnotherTreeNode_MovesTheHighlightAndTheFolder() {
        await SettleAsync();
        _page.Reveal(_first);

        _page.RootNodes[1].IsSelected = true;

        Assert.Equal(_second, _page.CurrentPath);
        Assert.False(_page.RootNodes[0].IsSelected);
        Assert.Same(_page.RootNodes[1], _page.SelectedNode);
    }

    // FileEntry glyphs need a render backend these tests lack, so a load faults after its read is counted.
    private async Task SettleAsync() {
        try {
            await _page.PendingLoads;
        } catch (TypeInitializationException) {
            // The glyph table failing to load, as above.
        }
    }

    private sealed class FixedRoots(params string[] paths) : IFileSystemRoots {
        public IReadOnlyList<DriveEntry> Read() {
            var roots = new List<DriveEntry>(paths.Length);
            foreach (var path in paths)
                roots.Add(new DriveEntry(Path.GetFileName(path), path, false));
            return roots;
        }
    }
}
