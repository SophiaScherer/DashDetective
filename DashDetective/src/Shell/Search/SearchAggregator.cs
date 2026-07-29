using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DashDetective.Shell.Search;

/// <summary>
/// Fans one query out to every provider, then merges what comes back into a single ordered list.
///
/// Providers run concurrently and independently: one throwing (the Windows index isn't running, a
/// process exited mid-scan) costs its own category and nothing else, following the same soft-fail rule
/// the rest of the app's providers keep. Cancellation is the aggregator's other job — the user types
/// faster than the filesystem answers, so a result set for a superseded term is dropped rather than
/// flashed on screen.
/// </summary>
public sealed class SearchAggregator {
    private readonly IReadOnlyList<ISearchProvider> _providers;

    public SearchAggregator(IReadOnlyList<ISearchProvider> providers) => _providers = providers;

    /// <summary>Runs the query and returns the merged, capped, best-first results. An empty term (or a
    /// cancelled query) yields an empty list rather than everything.</summary>
    public async Task<IReadOnlyList<SearchResult>> QueryAsync(SearchQuery query, CancellationToken token) {
        if (query.IsEmpty || _providers.Count == 0)
            return [];

        var pending = new Task<IReadOnlyList<SearchResult>>[_providers.Count];
        for (var i = 0; i < _providers.Count; i++)
            pending[i] = SafeQueryAsync(_providers[i], query, token);

        var batches = await Task.WhenAll(pending);

        // The term changed while the providers were working, so this answer describes something the user
        // is no longer asking about.
        if (token.IsCancellationRequested)
            return [];

        return Merge(batches, query);
    }

    // Each provider's best few, then the whole lot re-ordered and capped. The per-category cap is
    // applied here as well as inside the providers so one chatty source can't crowd out the rest.
    private static List<SearchResult> Merge(IReadOnlyList<SearchResult>[] batches, SearchQuery query) {
        var merged = new List<SearchResult>();

        foreach (var batch in batches) {
            if (batch.Count == 0)
                continue;

            var ranked = new List<SearchResult>(batch);
            ranked.Sort(Compare);

            var take = Math.Min(query.PerCategoryLimit, ranked.Count);
            for (var i = 0; i < take; i++) {
                // Sorted best-first, so the first non-match ends the batch.
                if (ranked[i].Score <= SearchRanker.NoMatch)
                    break;
                merged.Add(ranked[i]);
            }
        }

        merged.Sort(Compare);
        if (merged.Count > query.TotalLimit)
            merged.RemoveRange(query.TotalLimit, merged.Count - query.TotalLimit);

        return merged;
    }

    // Strongest match first; ties broken by category (the declaration order the dropdown groups by) and
    // then by title, so an unchanged query always produces an identically ordered list.
    private static int Compare(SearchResult a, SearchResult b) {
        var cmp = b.Score.CompareTo(a.Score);
        if (cmp != 0)
            return cmp;

        cmp = a.Category.CompareTo(b.Category);
        return cmp != 0 ? cmp : string.Compare(a.Title, b.Title, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<IReadOnlyList<SearchResult>> SafeQueryAsync(
        ISearchProvider provider, SearchQuery query, CancellationToken token) {
        try {
            return await provider.QueryAsync(query, token);
        } catch {
            // Includes the cancellation a provider raises when the term is superseded; QueryAsync
            // re-checks the token afterwards and discards the whole batch anyway.
            return [];
        }
    }
}
