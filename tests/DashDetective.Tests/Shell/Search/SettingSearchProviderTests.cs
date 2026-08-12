using DashDetective.Shell.Search;
using DashDetective.Shell.Search.Providers;
using DashDetective.Tabs.Settings;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DashDetective.Tests.Shell.Search;

/// <summary>Covers <see cref="SettingSearchProvider"/>: a setting is reachable by its label, its
/// section, its description or a synonym the page never shows, the label outranks the rest, and picking
/// a result asks for the right setting.</summary>
public class SettingSearchProviderTests {
    private static Task<IReadOnlyList<SearchResult>> Query(
        string term, System.Action<SettingId>? reveal = null) =>
        new SettingSearchProvider(reveal ?? (_ => { }))
            .QueryAsync(new SearchQuery(term), CancellationToken.None);

    private static async Task<SearchResult> Best(string term) {
        var results = await Query(term);
        return results.OrderByDescending(r => r.Score).First();
    }

    [Fact]
    public async Task QueryAsync_FindsASettingByItsLabel() {
        Assert.Equal("Theme", (await Best("theme")).Title);
    }

    [Fact]
    public async Task QueryAsync_FindsASettingByAKeywordThePageNeverShows() {
        // Neither "dark" nor "mode" appears beside the Theme picker, but both are what a user types.
        Assert.Equal("Theme", (await Best("dark mode")).Title);
        Assert.Equal("Launch at startup", (await Best("autostart")).Title);
    }

    [Fact]
    public async Task QueryAsync_FindsASettingByItsDescription() {
        Assert.Equal("Resource alerts", (await Best("exceeds 90%")).Title);
    }

    [Fact]
    public async Task QueryAsync_FindsEverySettingInASection() {
        var results = await Query("Monitoring");

        Assert.Equal(5, results.Count);
        Assert.All(results, r => Assert.Equal("Monitoring", r.Subtitle));
    }

    [Fact]
    public async Task QueryAsync_RanksTheLabelAboveTheDescription() {
        // "colour"/"color" appears in the Theme description and in the Accent label + keywords.
        var results = await Query("accent color");
        var ordered = results.OrderByDescending(r => r.Score).ToList();

        Assert.Equal("Accent color", ordered[0].Title);
    }

    [Fact]
    public async Task QueryAsync_ActivatingAResultAsksForThatSetting() {
        SettingId? revealed = null;
        var results = await Query("tray", id => revealed = id);

        results.OrderByDescending(r => r.Score).First().Activate();

        Assert.Equal(SettingId.ShowInTray, revealed);
    }

    [Fact]
    public async Task QueryAsync_NamesTheSectionAsTheSubtitle() {
        Assert.Equal("Appearance", (await Best("theme")).Subtitle);
    }

    [Fact]
    public async Task QueryAsync_TagsEveryResultAsASetting() {
        var results = await Query("e");

        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.Equal(SearchCategory.Setting, r.Category));
    }

    [Fact]
    public async Task QueryAsync_ReturnsNothingForATermNoSettingMatches() {
        Assert.Empty(await Query("zzzz"));
    }
}
