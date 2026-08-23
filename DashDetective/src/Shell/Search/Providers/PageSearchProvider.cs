using DashDetective.Shell.Navigation;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DashDetective.Shell.Search.Providers;

/// <summary>
/// Finds the app's own tabs. The nav bar is already a data-driven list carrying a label, a title, a
/// subtitle and an icon per page, so this reads that same list rather than restating it — a tab added
/// to the bar becomes searchable with no change here.
/// </summary>
public sealed class PageSearchProvider : ISearchProvider {
    private readonly IReadOnlyList<NavItem> _items;
    private readonly Action<NavItem> _navigate;

    public PageSearchProvider(IReadOnlyList<NavItem> items, Action<NavItem> navigate) {
        _items = items;
        _navigate = navigate;
    }

    public SearchCategory Category => SearchCategory.Page;

    // The token is accepted but not checked: this scan is a synchronous pass over an in-memory table
    // that returns before a cancellation could arrive. The aggregator re-checks the token after the
    // fan-out, so a superseded query still discards these results.
    public Task<IReadOnlyList<SearchResult>> QueryAsync(SearchQuery query, CancellationToken token) {
        var term = query.Term;
        var results = new List<SearchResult>();

        foreach (var item in _items) {
            // The subtitle is the weakest field: "Live processes & resource usage" shouldn't outrank the
            // page actually called Processes.
            var score = SearchRanker.ScoreBest(term, item.Label, item.Title, item.Subtitle);
            if (score == SearchRanker.NoMatch)
                continue;

            var page = item;
            results.Add(new SearchResult(
                SearchCategory.Page, item.Title, item.Subtitle, score,
                () => _navigate(page), item.Icon, item.Label));
        }

        return Task.FromResult<IReadOnlyList<SearchResult>>(results);
    }
}
