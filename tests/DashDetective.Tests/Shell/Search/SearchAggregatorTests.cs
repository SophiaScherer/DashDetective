using DashDetective.Shell.Search;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DashDetective.Tests.Shell.Search;

/// <summary>Covers <see cref="SearchAggregator"/>: several independent providers merge into one
/// best-first list, one failing provider costs only its own category, the caps hold, and a query the
/// user has already typed past is discarded rather than shown.</summary>
public class SearchAggregatorTests {
    /// <summary>A provider that answers with a fixed set, optionally after a delay or by throwing.</summary>
    private sealed class FakeProvider : ISearchProvider {
        private readonly IReadOnlyList<SearchResult> _results;
        private readonly Exception? _failure;

        public FakeProvider(SearchCategory category, params SearchResult[] results) {
            Category = category;
            _results = results;
        }

        private FakeProvider(SearchCategory category, Exception failure) {
            Category = category;
            _results = [];
            _failure = failure;
        }

        public static FakeProvider Failing(SearchCategory category) =>
            new(category, new InvalidOperationException("provider is unavailable"));

        public SearchCategory Category { get; }

        public bool WasQueried { get; private set; }

        public Task<IReadOnlyList<SearchResult>> QueryAsync(SearchQuery query, CancellationToken token) {
            WasQueried = true;
            return _failure is null
                ? Task.FromResult(_results)
                : Task.FromException<IReadOnlyList<SearchResult>>(_failure);
        }
    }

    private static SearchResult Result(SearchCategory category, string title, int score) =>
        new(category, title, "", score, () => { });

    [Fact]
    public async Task QueryAsync_MergesEveryProviderIntoOneBestFirstList() {
        var aggregator = new SearchAggregator([
            new FakeProvider(SearchCategory.File, Result(SearchCategory.File, "middling", 500)),
            new FakeProvider(SearchCategory.Page, Result(SearchCategory.Page, "strongest", 900)),
            new FakeProvider(SearchCategory.Process, Result(SearchCategory.Process, "weakest", 100)),
        ]);

        var results = await aggregator.QueryAsync(new SearchQuery("x"), CancellationToken.None);

        Assert.Equal(["strongest", "middling", "weakest"], results.Select(r => r.Title));
    }

    [Fact]
    public async Task QueryAsync_BreaksScoreTiesByCategoryThenTitle() {
        var aggregator = new SearchAggregator([
            new FakeProvider(SearchCategory.File,
                Result(SearchCategory.File, "beta", 500), Result(SearchCategory.File, "alpha", 500)),
            new FakeProvider(SearchCategory.Page, Result(SearchCategory.Page, "zeta", 500)),
        ]);

        var results = await aggregator.QueryAsync(new SearchQuery("x"), CancellationToken.None);

        // Page is declared before File, so it leads despite the later title.
        Assert.Equal(["zeta", "alpha", "beta"], results.Select(r => r.Title));
    }

    [Fact]
    public async Task QueryAsync_KeepsGoingWhenOneProviderFails() {
        var healthy = new FakeProvider(SearchCategory.Page, Result(SearchCategory.Page, "survivor", 900));
        var aggregator = new SearchAggregator([FakeProvider.Failing(SearchCategory.File), healthy]);

        var results = await aggregator.QueryAsync(new SearchQuery("x"), CancellationToken.None);

        Assert.Equal("survivor", Assert.Single(results).Title);
    }

    [Fact]
    public async Task QueryAsync_CapsEachCategorySoOneSourceCannotCrowdOutTheRest() {
        var noisy = Enumerable.Range(0, 10)
            .Select(i => Result(SearchCategory.File, $"file{i}", 900))
            .ToArray();
        var aggregator = new SearchAggregator([
            new FakeProvider(SearchCategory.File, noisy),
            new FakeProvider(SearchCategory.Page, Result(SearchCategory.Page, "page", 100)),
        ]);

        var results = await aggregator.QueryAsync(
            new SearchQuery("x", PerCategoryLimit: 3), CancellationToken.None);

        Assert.Equal(3, results.Count(r => r.Category == SearchCategory.File));
        Assert.Contains(results, r => r.Category == SearchCategory.Page);
    }

    [Fact]
    public async Task QueryAsync_CapsTheMergedList() {
        var aggregator = new SearchAggregator([
            new FakeProvider(SearchCategory.File,
                Enumerable.Range(0, 5).Select(i => Result(SearchCategory.File, $"f{i}", 900 - i)).ToArray()),
            new FakeProvider(SearchCategory.Page,
                Enumerable.Range(0, 5).Select(i => Result(SearchCategory.Page, $"p{i}", 500 - i)).ToArray()),
        ]);

        var results = await aggregator.QueryAsync(
            new SearchQuery("x", PerCategoryLimit: 5, TotalLimit: 4), CancellationToken.None);

        // The cap keeps the strongest four, which are all files here.
        Assert.Equal(4, results.Count);
        Assert.All(results, r => Assert.Equal(SearchCategory.File, r.Category));
    }

    [Fact]
    public async Task QueryAsync_DropsResultsThatScoredNoMatch() {
        var aggregator = new SearchAggregator([
            new FakeProvider(SearchCategory.Page,
                Result(SearchCategory.Page, "real", 700),
                Result(SearchCategory.Page, "unmatched", SearchRanker.NoMatch)),
        ]);

        var results = await aggregator.QueryAsync(new SearchQuery("x"), CancellationToken.None);

        Assert.Equal("real", Assert.Single(results).Title);
    }

    [Fact]
    public async Task QueryAsync_DiscardsAnAnswerTheUserHasAlreadyTypedPast() {
        var aggregator = new SearchAggregator([
            new FakeProvider(SearchCategory.Page, Result(SearchCategory.Page, "stale", 900)),
        ]);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.Empty(await aggregator.QueryAsync(new SearchQuery("x"), cts.Token));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task QueryAsync_SkipsTheProvidersEntirelyForAnEmptyTerm(string term) {
        var provider = new FakeProvider(SearchCategory.Page, Result(SearchCategory.Page, "page", 900));
        var aggregator = new SearchAggregator([provider]);

        Assert.Empty(await aggregator.QueryAsync(new SearchQuery(term), CancellationToken.None));
        Assert.False(provider.WasQueried);
    }

    [Fact]
    public async Task QueryAsync_ReturnsEmptyWithNoProvidersConfigured() {
        var aggregator = new SearchAggregator([]);

        Assert.Empty(await aggregator.QueryAsync(new SearchQuery("x"), CancellationToken.None));
    }
}
