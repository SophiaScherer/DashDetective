using Avalonia.Media;
using DashDetective.Shell.Help;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DashDetective.Shell.Search.Providers;

/// <summary>
/// Finds Help's own copy — the page tour and the orientation tips — so "dock the navigation bar"
/// answers with the sentence that explains it rather than nothing at all. Reads
/// <see cref="HelpContent"/>, which is what the modal renders, so the copy searched is by construction
/// the copy displayed.
///
/// Picking one opens Help on the topic's own tab and flashes it, the same landing the Settings results
/// give. The shortcut table is not searched here: <c>ShortcutSearchProvider</c> already covers it, and
/// listing a binding twice under two category tags would only crowd the dropdown.
/// </summary>
public sealed class HelpSearchProvider : ISearchProvider {
    private readonly Action<HelpTab, string> _reveal;
    private readonly Geometry? _icon;

    /// <param name="reveal">Opens Help on a tab with the topic revealed.</param>
    /// <param name="icon">The row glyph. Passed in rather than read from the nav <c>Icons</c> table,
    /// which cannot be touched without a render backend attached.</param>
    public HelpSearchProvider(Action<HelpTab, string> reveal, Geometry? icon = null) {
        _reveal = reveal;
        _icon = icon;
    }

    public SearchCategory Category => SearchCategory.Help;

    // The token is accepted but not checked: this scan is a synchronous pass over an in-memory table
    // that returns before a cancellation could arrive. The aggregator re-checks the token after the
    // fan-out, so a superseded query still discards these results.
    public Task<IReadOnlyList<SearchResult>> QueryAsync(SearchQuery query, CancellationToken token) {
        var results = new List<SearchResult>();

        Collect(query.Term, HelpContent.GettingStarted, HelpTab.GettingStarted, "Getting started", results);
        Collect(query.Term, HelpContent.Tips, HelpTab.Tips, "Tips", results);

        return Task.FromResult<IReadOnlyList<SearchResult>>(results);
    }

    private void Collect(
        string term, IReadOnlyList<HelpTopic> topics, HelpTab tab, string section,
        List<SearchResult> results) {
        foreach (var topic in topics) {
            // Only titled topics score across two fields: handing ScoreBest a null title would spend a
            // field penalty on it and quietly rank every tip below the tour.
            var score = topic.Title is null
                ? SearchRanker.Score(term, topic.Body)
                : SearchRanker.ScoreBest(term, topic.Title, topic.Body);
            if (score == SearchRanker.NoMatch)
                continue;

            var key = topic.Key;
            results.Add(new SearchResult(
                SearchCategory.Help, topic.Title ?? topic.Body, section, score,
                () => _reveal(tab, key), _icon, Key: key));
        }
    }
}
