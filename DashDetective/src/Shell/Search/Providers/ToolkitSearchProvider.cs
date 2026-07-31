using Avalonia.Media;
using DashDetective.Tabs.Toolkit;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DashDetective.Shell.Search.Providers;

/// <summary>
/// Finds Toolkit commands by the command itself or by what it does, so "flush dns" reaches the command
/// that does it without the user knowing its name. Both fields come from <see cref="ToolkitCatalog"/>,
/// which the rows themselves bind to, so the copy searched is the copy displayed.
///
/// Picking one navigates to the Toolkit tab, clears its filter so the row is definitely on the page,
/// and flashes it — the <see cref="SettingSearchProvider"/> pattern.
/// </summary>
public sealed class ToolkitSearchProvider : ISearchProvider {
    private readonly Func<IReadOnlyList<ToolkitEntry>> _entries;
    private readonly Action<string> _reveal;
    private readonly Geometry? _icon;

    /// <param name="entries">The command set to search. Passed as a callback so the provider reads
    /// whatever the catalog holds at query time rather than a copy taken at startup.</param>
    /// <param name="reveal">Navigates to the Toolkit tab and reveals the command.</param>
    /// <param name="icon">The row glyph. Passed in rather than read from the nav <c>Icons</c> table,
    /// which cannot be touched without a render backend attached.</param>
    public ToolkitSearchProvider(
        Func<IReadOnlyList<ToolkitEntry>> entries, Action<string> reveal, Geometry? icon = null) {
        _entries = entries;
        _reveal = reveal;
        _icon = icon;
    }

    public SearchCategory Category => SearchCategory.Toolkit;

    public Task<IReadOnlyList<SearchResult>> QueryAsync(SearchQuery query, CancellationToken token) {
        var term = query.Term;
        var results = new List<SearchResult>();

        foreach (var entry in _entries()) {
            // The command outranks its description: someone typing "ipconfig" wants that command, not
            // every command whose description happens to mention it.
            var score = SearchRanker.ScoreBest(term, entry.Command, entry.Description);
            if (score == SearchRanker.NoMatch)
                continue;

            var command = entry.Command;
            results.Add(new SearchResult(
                SearchCategory.Toolkit, command, entry.Description, score,
                () => _reveal(command), _icon, command, Key: command));
        }

        return Task.FromResult<IReadOnlyList<SearchResult>>(results);
    }
}
