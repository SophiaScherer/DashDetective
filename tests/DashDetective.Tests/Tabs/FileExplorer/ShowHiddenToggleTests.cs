using DashDetective.Tabs.FileExplorer;
using DashDetective.Tests.Fakes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace DashDetective.Tests.Tabs.FileExplorer;

/// <summary>Pins that the newest folder read wins when Show hidden is toggled faster than reads finish.</summary>
public sealed class ShowHiddenToggleTests : IDisposable {
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "DashDetectiveTests_" + Path.GetRandomFileName());

    private readonly List<(bool IncludeHidden, TaskCompletionSource<FolderRead> Result)> _reads = new();
    private readonly FileExplorerViewModel _page;

    public ShowHiddenToggleTests() {
        Directory.CreateDirectory(_root);
        _page = new FileExplorerViewModel(new FakeShellInterop(), new UnsupportedFileSystemRoots(), Read);
    }

    public void Dispose() {
        _page.Dispose();
        try {
            Directory.Delete(_root, recursive: true);
        } catch (IOException) {
            // A watcher may still hold the folder; the temp directory is fine to leave.
        }
    }

    private Task<FolderRead> Read(string path, bool includeHidden, IShellInterop shell) {
        var result = new TaskCompletionSource<FolderRead>();
        _reads.Add((includeHidden, result));
        return result.Task;
    }

    // Rows need a render backend, so the two reads are told apart by their status's folder message.
    private static FolderRead Empty(FolderReadStatus status) => new(Array.Empty<FileItem>(), status);

    [Fact]
    public async Task ShowHidden_ToggledOnThenOff_LastRequestWinsWhenReadsFinishOutOfOrder() {
        _page.Reveal(_root);
        _page.ShowHidden = true;
        _page.ShowHidden = false;
        Assert.Equal(3, _reads.Count);
        Assert.False(_reads[2].IncludeHidden);

        // The unchecked read finishes first, then the stale checked one lands.
        _reads[2].Result.SetResult(Empty(FolderReadStatus.Ok));
        await WaitUntil(() => _page.FolderMessageTitle.Length > 0);
        var expected = _page.FolderMessageTitle;
        _reads[1].Result.SetResult(Empty(FolderReadStatus.AccessDenied));
        await Task.Delay(200);

        Assert.NotEmpty(expected);
        Assert.Equal(expected, _page.FolderMessageTitle);
    }

    private static async Task WaitUntil(Func<bool> condition) {
        for (var i = 0; i < 200 && !condition(); i++)
            await Task.Delay(10);
    }
}
