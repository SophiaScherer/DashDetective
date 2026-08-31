using DashDetective.Shell.Help;
using DashDetective.Shell.Search;
using DashDetective.Shell.Search.Providers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DashDetective.Tests.Shell.Search;

/// <summary>Covers <see cref="HelpSearchProvider"/>: Help's own copy is reachable by a page name or by
/// words buried in a tip, each result names its section, and picking one asks for that topic's tab.</summary>
public class HelpSearchProviderTests {
    private static Task<IReadOnlyList<SearchResult>> Query(
        string term, Action<HelpTab, string>? reveal = null) =>
        new HelpSearchProvider(reveal ?? ((_, _) => { }))
            .QueryAsync(new SearchQuery(term), CancellationToken.None);

    private static async Task<SearchResult> Best(string term) {
        var results = await Query(term);
        return results.OrderByDescending(r => r.Score).First();
    }

    [Fact]
    public async Task QueryAsync_FindsAPageByItsName() {
        var best = await Best("File Explorer");

        Assert.Equal("File Explorer", best.Title);
        Assert.Equal("Getting started", best.Subtitle);
    }

    [Fact]
    public async Task QueryAsync_FindsATipByWordsInsideIt() {
        var results = await Query("dock it to any window edge");

        Assert.Contains(results, r => r.Subtitle == "Tips");
    }

    [Fact]
    public async Task QueryAsync_TitlesAnUntitledTipWithItsBody() {
        var tip = HelpContent.Tips.First(t => t.Key == "tip.completion");
        var results = await Query("grayed out after the caret");

        Assert.Contains(results, r => r.Title == tip.Body);
    }

    [Fact]
    public async Task QueryAsync_RanksAPageNameAboveAMentionInsideATip() {
        // "Performance" is both a page title and a word inside the tip about detailed graphs.
        Assert.Equal("Getting started", (await Best("Performance")).Subtitle);
    }

    [Fact]
    public async Task QueryAsync_TagsEveryResultAsHelp() {
        var results = await Query("the");

        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.Equal(SearchCategory.Help, r.Category));
    }

    [Fact]
    public async Task QueryAsync_CarriesTheTopicKeyAsTheResultIdentity() {
        var results = await Query("File Explorer");

        Assert.Contains(results, r => r.Identity == "page.fileExplorer");
    }

    [Fact]
    public async Task QueryAsync_ActivatingAResultAsksForThatTopicsTab() {
        HelpTab? tab = null;
        string? key = null;
        var results = await Query("grayed out after the caret", (t, k) => { tab = t; key = k; });

        results.OrderByDescending(r => r.Score).First().Activate();

        Assert.Equal(HelpTab.Tips, tab);
        Assert.Equal("tip.completion", key);
    }

    [Fact]
    public async Task QueryAsync_ActivatingAPageResultAsksForGettingStarted() {
        HelpTab? tab = null;
        var results = await Query("Toolkit", (t, _) => tab = t);

        results.OrderByDescending(r => r.Score).First().Activate();

        Assert.Equal(HelpTab.GettingStarted, tab);
    }

    [Fact]
    public async Task QueryAsync_ReturnsNothingForATermNoTopicMatches() {
        Assert.Empty(await Query("zzzz"));
    }
}
