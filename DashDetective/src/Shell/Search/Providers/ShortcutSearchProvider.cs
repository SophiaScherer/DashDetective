using Avalonia.Media;
using DashDetective.Shared.Shortcuts;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DashDetective.Shell.Search.Providers;

/// <summary>
/// Finds keyboard shortcuts by what they do, so "export" turns up Ctrl+E without the user having to
/// remember it. Reads <see cref="ShortcutCatalog.HelpGroups"/> rather than the flat table: the groups
/// already carry the scope's display title and already exclude the entries Help hides, so a result's
/// subtitle reads "General · Ctrl+E" with nothing restated here.
///
/// Picking one opens Help rather than firing the shortcut — the answer to "how do I export?" is the
/// binding, and firing it blind from a search box would be a surprising way to trigger an action.
/// </summary>
public sealed class ShortcutSearchProvider : ISearchProvider {
    private readonly ShortcutBindings _shortcuts;
    private readonly Action _openHelp;
    private readonly Geometry? _icon;

    /// <summary>The row glyph is passed in rather than read from the nav <c>Icons</c> table: a
    /// <c>Geometry</c> can only be built with a render backend attached, so reaching for one here would
    /// make this class untestable headlessly.</summary>
    public ShortcutSearchProvider(ShortcutBindings shortcuts, Action openHelp, Geometry? icon = null) {
        _shortcuts = shortcuts;
        _openHelp = openHelp;
        _icon = icon;
    }

    public SearchCategory Category => SearchCategory.Shortcut;

    // The token is accepted but not checked: this scan is a synchronous pass over an in-memory table
    // that returns before a cancellation could arrive. The aggregator re-checks the token after the
    // fan-out, so a superseded query still discards these results.
    public Task<IReadOnlyList<SearchResult>> QueryAsync(SearchQuery query, CancellationToken token) {
        var term = query.Term;
        var results = new List<SearchResult>();

        foreach (var group in _shortcuts.HelpGroups)
            foreach (var shortcut in group.Shortcuts) {
                var score = SearchRanker.ScoreBest(term, shortcut.Description, shortcut.Keys);
                if (score == SearchRanker.NoMatch)
                    continue;

                results.Add(new SearchResult(
                    SearchCategory.Shortcut, shortcut.Description, $"{group.Title} · {shortcut.Keys}",
                    score, _openHelp, _icon));
            }

        return Task.FromResult<IReadOnlyList<SearchResult>>(results);
    }
}
