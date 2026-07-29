using Avalonia.Media;
using DashDetective.Tabs.Settings;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DashDetective.Shell.Search.Providers;

/// <summary>
/// Finds settings by their label, their section, their description, or a synonym the page never shows —
/// "dark mode" reaches the Theme picker even though neither word appears beside it. All four come from
/// <see cref="SettingCatalog"/>, which the page's own labels bind to, so the copy searched is by
/// construction the copy displayed.
///
/// Picking one navigates to Settings and asks the page to scroll that row into view and flash it, so
/// the answer to "where do I turn off the alert banner?" is the control itself rather than a page of
/// toggles to hunt through.
/// </summary>
public sealed class SettingSearchProvider : ISearchProvider {
    private readonly Action<SettingId> _reveal;
    private readonly Geometry? _icon;

    /// <param name="reveal">Navigates to Settings and reveals the setting.</param>
    /// <param name="icon">The row glyph. Passed in rather than read from the nav <c>Icons</c> table,
    /// which cannot be touched without a render backend attached.</param>
    public SettingSearchProvider(Action<SettingId> reveal, Geometry? icon = null) {
        _reveal = reveal;
        _icon = icon;
    }

    public SearchCategory Category => SearchCategory.Setting;

    public Task<IReadOnlyList<SearchResult>> QueryAsync(SearchQuery query, CancellationToken token) {
        var term = query.Term;
        var results = new List<SearchResult>();

        foreach (var entry in SettingCatalog.Instance.All) {
            // Keywords rank just below the label: they exist to catch the words a user actually types,
            // so demoting them as far as the description would defeat the point of having them.
            var score = SearchRanker.ScoreBest(
                term, entry.Name, entry.Keywords, entry.Section, entry.Description);
            if (score == SearchRanker.NoMatch)
                continue;

            var id = entry.Id;
            results.Add(new SearchResult(
                SearchCategory.Setting, entry.Name, entry.Section, score,
                () => _reveal(id), _icon, entry.Name));
        }

        return Task.FromResult<IReadOnlyList<SearchResult>>(results);
    }
}
