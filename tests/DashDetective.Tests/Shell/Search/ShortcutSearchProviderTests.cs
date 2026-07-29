using DashDetective.Shared.Shortcuts;
using DashDetective.Shell.Search;
using DashDetective.Shell.Search.Providers;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DashDetective.Tests.Shell.Search;

/// <summary>Covers <see cref="ShortcutSearchProvider"/>: shortcuts are findable by what they do rather
/// than by the keys, every result is described by the catalog it came from, and picking one shows the
/// binding in Help instead of firing the action blind.</summary>
public class ShortcutSearchProviderTests {
    private static Task<IReadOnlyList<SearchResult>> Query(string term, System.Action? openHelp = null) =>
        new ShortcutSearchProvider(openHelp ?? (() => { }))
            .QueryAsync(new SearchQuery(term), CancellationToken.None);

    [Fact]
    public async Task QueryAsync_FindsAShortcutByWhatItDoes() {
        var results = await Query("export");

        Assert.Contains(results, r => r.Subtitle.Contains("Ctrl+E"));
    }

    [Fact]
    public async Task QueryAsync_FindsAShortcutByItsKeys() {
        var results = await Query("F5");

        Assert.Contains(results, r => r.Title.Contains("Refresh", System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task QueryAsync_NamesTheScopeAndTheKeysInTheSubtitle() {
        var results = await Query("end the selected process");

        var result = Assert.Single(results);
        Assert.Equal("Processes · Delete", result.Subtitle);
    }

    [Fact]
    public async Task QueryAsync_OpensHelpRatherThanFiringTheShortcut() {
        var opened = 0;
        var results = await Query("export", () => opened++);

        results[0].Activate();

        Assert.Equal(1, opened);
    }

    [Fact]
    public async Task QueryAsync_SkipsTheEntriesHelpItselfHides() {
        // Ctrl+2…Ctrl+8 are covered by the Ctrl+1 row and carry no copy of their own, so they must not
        // surface as blank rows.
        var results = await Query("tab");

        Assert.All(results, r => Assert.False(string.IsNullOrWhiteSpace(r.Title)));
        Assert.Equal(
            ShortcutCatalog.All.Count(s => s.ShowInHelp),
            ShortcutCatalog.HelpGroups.Sum(g => g.Shortcuts.Count));
    }

    [Fact]
    public async Task QueryAsync_TagsEveryResultAsAShortcut() {
        var results = await Query("tab");

        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.Equal(SearchCategory.Shortcut, r.Category));
    }

    [Fact]
    public async Task QueryAsync_ReturnsNothingForATermNoShortcutMatches() {
        Assert.Empty(await Query("zzzz"));
    }
}
