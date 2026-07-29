using DashDetective.Services.Search;
using DashDetective.Shell.Search;
using DashDetective.Shell.Search.Providers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DashDetective.Tests.Shell.Search;

/// <summary>Covers <see cref="FileSearchProvider"/>: it prefers the index, falls back only when the
/// index cannot answer (not merely when it found nothing), ranks hits by name, and hands back a
/// callback that reveals the right path.</summary>
public class FileSearchProviderTests {
    /// <summary>A file source that answers with a fixed set, or with null to mean "unavailable".</summary>
    private sealed class StubSource : IFileSearch {
        private readonly IReadOnlyList<FileHit>? _hits;

        public StubSource(IReadOnlyList<FileHit>? hits) => _hits = hits;

        public static StubSource Unavailable => new(null);

        public int Calls { get; private set; }
        public int LastLimit { get; private set; }

        public Task<IReadOnlyList<FileHit>?> SearchAsync(
            string term, IReadOnlyList<string> scopes, int limit, CancellationToken token) {
            Calls++;
            LastLimit = limit;
            return Task.FromResult(_hits);
        }
    }

    private static FileHit Hit(string name, bool isDirectory = false) =>
        new(name, @"C:\Users\Sophia\Docs\" + name, @"C:\Users\Sophia\Docs", isDirectory, DateTime.Now);

    private static FileSearchProvider Provider(
        IFileSearch index, IFileSearch fallback, Action<string>? reveal = null) =>
        new(index, fallback, () => null, reveal ?? (_ => { }));

    private static Task<IReadOnlyList<SearchResult>> Query(FileSearchProvider provider, string term) =>
        provider.QueryAsync(new SearchQuery(term), CancellationToken.None);

    [Fact]
    public async Task QueryAsync_UsesTheIndexAndLeavesTheScanAlone() {
        var index = new StubSource([Hit("report.txt")]);
        var fallback = new StubSource([Hit("wrong.txt")]);

        var results = await Query(Provider(index, fallback), "report");

        Assert.Equal("report.txt", Assert.Single(results).Title);
        Assert.Equal(0, fallback.Calls);
    }

    [Fact]
    public async Task QueryAsync_FallsBackWhenTheIndexCannotAnswer() {
        var fallback = new StubSource([Hit("report.txt")]);

        var results = await Query(Provider(StubSource.Unavailable, fallback), "report");

        Assert.Equal("report.txt", Assert.Single(results).Title);
        Assert.Equal(1, fallback.Calls);
    }

    [Fact]
    public async Task QueryAsync_TrustsAnEmptyIndexAnswerRatherThanRescanningTheDisk() {
        // "Nothing matched" is the honest answer; only "I can't answer" is worth falling back from.
        var fallback = new StubSource([Hit("report.txt")]);

        var results = await Query(Provider(new StubSource([]), fallback), "report");

        Assert.Empty(results);
        Assert.Equal(0, fallback.Calls);
    }

    [Fact]
    public async Task QueryAsync_ReturnsNothingWhenNeitherSourceCanAnswer() {
        var results = await Query(Provider(StubSource.Unavailable, StubSource.Unavailable), "report");

        Assert.Empty(results);
    }

    [Fact]
    public async Task QueryAsync_ScoresACloserNameMatchHigher() {
        // Ordering itself is the aggregator's job (it merges every category into one list); the
        // provider's part is scoring each hit so that merge has something honest to sort on.
        var index = new StubSource([Hit("quarterly-report-draft.txt"), Hit("report.txt")]);

        var results = await Query(Provider(index, StubSource.Unavailable), "report");
        var best = results.OrderByDescending(r => r.Score).First();

        Assert.Equal("report.txt", best.Title);
    }

    [Fact]
    public async Task QueryAsync_DropsAHitWhoseNameDoesNotActuallyMatch() {
        // The index matches on indexed content and metadata as well as the name, so it can return a
        // file the term appears nowhere in the name of.
        var index = new StubSource([Hit("report.txt"), Hit("unrelated.txt")]);

        var results = await Query(Provider(index, StubSource.Unavailable), "report");

        Assert.Equal("report.txt", Assert.Single(results).Title);
    }

    [Fact]
    public async Task QueryAsync_ActivatingAResultRevealsItsFullPath() {
        string? revealed = null;
        var index = new StubSource([Hit("report.txt")]);

        var results = await Query(Provider(index, StubSource.Unavailable, p => revealed = p), "report");
        results[0].Activate();

        Assert.Equal(@"C:\Users\Sophia\Docs\report.txt", revealed);
    }

    [Fact]
    public async Task QueryAsync_NamesTheContainingFolderAsTheSubtitle() {
        var index = new StubSource([Hit("report.txt")]);

        var results = await Query(Provider(index, StubSource.Unavailable), "report");

        Assert.Equal(@"C:\Users\Sophia\Docs", results[0].Subtitle);
    }

    [Fact]
    public async Task QueryAsync_AsksForMoreHitsThanItWillShow() {
        // The index orders by modified date, so its first rows are not necessarily the best name
        // matches — ranking needs a wider pool than the dropdown will display.
        var index = new StubSource([]);

        await Provider(index, StubSource.Unavailable)
            .QueryAsync(new SearchQuery("report", PerCategoryLimit: 5), CancellationToken.None);

        Assert.True(index.LastLimit > 5);
    }

    [Fact]
    public async Task QueryAsync_TagsEveryResultAsAFile() {
        var index = new StubSource([Hit("report.txt"), Hit("Reports", isDirectory: true)]);

        var results = await Query(Provider(index, StubSource.Unavailable), "report");

        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Equal(SearchCategory.File, r.Category));
    }
}
