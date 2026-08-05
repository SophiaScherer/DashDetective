using DashDetective.Services.Search;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Xunit;

namespace DashDetective.Tests.Services.Search;

/// <summary>Covers <see cref="FileSystemFallbackSearch"/> against a real temp tree: it goes wide before
/// deep (so the first results are the shallow ones worth showing), honours its depth, result and
/// folder-count caps, and abandons the walk the moment the term changes.</summary>
public sealed class FileSystemFallbackSearchTests : IDisposable {
    private readonly string _root;

    public FileSystemFallbackSearchTests() {
        _root = Path.Combine(Path.GetTempPath(), "DashDetectiveTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose() {
        try {
            Directory.Delete(_root, recursive: true);
        } catch {
            // Best-effort cleanup of the temp directory.
        }
    }

    private string Dir(params string[] segments) {
        var path = Path.Combine([_root, .. segments]);
        Directory.CreateDirectory(path);
        return path;
    }

    private void File_(string name, params string[] folder) =>
        System.IO.File.WriteAllText(Path.Combine(folder.Length == 0 ? _root : Dir(folder), name), "x");

    // Windows marks a file hidden with an attribute; Unix does it with a leading dot, where
    // SetAttributes is a no-op. .NET's enumerator reports FileAttributes.Hidden for both, which is
    // what the scan actually skips on.
    private void HiddenFile_(string name) {
        if (!OperatingSystem.IsWindows()) {
            File_("." + name);
            return;
        }

        File_(name);
        System.IO.File.SetAttributes(Path.Combine(_root, name), FileAttributes.Hidden);
    }

    private IReadOnlyList<FileHit> Scan(string term, int limit = 20, CancellationToken token = default) =>
        FileSystemFallbackSearch.Scan(term, [_root], limit, token);

    [Fact]
    public void Scan_FindsAFileInTheScopeRoot() {
        File_("report.txt");

        var hit = Assert.Single(Scan("report"));

        Assert.Equal("report.txt", hit.Name);
        Assert.False(hit.IsDirectory);
        Assert.Equal(_root, hit.FolderPath);
    }

    [Fact]
    public void Scan_FindsAFileNestedInSubfolders() {
        File_("report.txt", "Documents", "Projects");

        var hit = Assert.Single(Scan("report"));

        Assert.Equal(Path.Combine(_root, "Documents", "Projects"), hit.FolderPath);
    }

    [Fact]
    public void Scan_FindsFoldersAsWellAsFiles() {
        Dir("Reports");

        var hit = Assert.Single(Scan("Reports"));

        Assert.True(hit.IsDirectory);
    }

    [Fact]
    public void Scan_MatchesAnywhereInTheNameAndIgnoresCase() {
        File_("Q4-REPORT-final.txt");

        Assert.Single(Scan("report"));
    }

    [Fact]
    public void Scan_GoesWideBeforeDeep() {
        // A shallow match must be found before a deep one, so a cut-short walk returns the results
        // most likely to be wanted.
        File_("report-deep.txt", "a", "b", "c");
        File_("report-shallow.txt");

        var names = Scan("report").Select(h => h.Name).ToList();

        Assert.Equal("report-shallow.txt", names[0]);
    }

    [Fact]
    public void Scan_StopsAtTheDepthCap() {
        var tooDeep = Enumerable.Range(0, FileSystemFallbackSearch.MaxDepth + 2)
            .Select(i => "d" + i).ToArray();
        File_("report.txt", tooDeep);

        Assert.Empty(Scan("report"));
    }

    [Fact]
    public void Scan_ReachesAFileAtTheDepthCap() {
        var atCap = Enumerable.Range(0, FileSystemFallbackSearch.MaxDepth).Select(i => "d" + i).ToArray();
        File_("report.txt", atCap);

        Assert.Single(Scan("report"));
    }

    [Fact]
    public void Scan_StopsAtTheResultCap() {
        for (var i = 0; i < 10; i++)
            File_($"report{i}.txt");

        Assert.Equal(3, Scan("report", limit: 3).Count);
    }

    [Fact]
    public void Scan_AbandonsAnAlreadyCancelledWalk() {
        File_("report.txt");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.Empty(Scan("report", token: cts.Token));
    }

    [Fact]
    public void Scan_SkipsHiddenEntries() {
        // Matching DirectoryService: the File Explorer hides these by default, so search must too.
        HiddenFile_("report-hidden.txt");

        Assert.Empty(Scan("report"));
    }

    [Fact]
    public void Scan_IgnoresAScopeThatDoesNotExist() {
        File_("report.txt");
        var missing = Path.Combine(_root, "gone");

        var hits = FileSystemFallbackSearch.Scan("report", [missing, _root], 20, default);

        Assert.Single(hits);
    }

    [Fact]
    public void Scan_VisitsAFolderOnceWhenTwoScopesOverlap() {
        File_("report.txt");

        var hits = FileSystemFallbackSearch.Scan("report", [_root, _root], 20, default);

        Assert.Single(hits);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Scan_ReturnsNothingForAnEmptyTerm(string term) {
        File_("report.txt");

        Assert.Empty(Scan(term));
    }

    [Fact]
    public void Scan_ReturnsNothingForANonPositiveLimit() {
        File_("report.txt");

        Assert.Empty(Scan("report", limit: 0));
    }

    [Fact]
    public void Scan_ReturnsNothingWhenNothingMatches() {
        File_("report.txt");

        Assert.Empty(Scan("zzzz"));
    }
}
