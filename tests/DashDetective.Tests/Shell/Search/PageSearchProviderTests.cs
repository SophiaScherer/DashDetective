using DashDetective.Shared;
using DashDetective.Shell.Navigation;
using DashDetective.Shell.Search;
using DashDetective.Shell.Search.Providers;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DashDetective.Tests.Shell.Search;

/// <summary>Covers <see cref="PageSearchProvider"/>: it reads the live nav bar rather than a copy, ranks
/// a page's own name above a word buried in its subtitle, and hands back a callback that navigates to
/// the matching tab.</summary>
public class PageSearchProviderTests {
    private sealed class StubPage : ViewModelBase;

    // The icon is only ever handed to the result row; a real Geometry needs a render backend, which a
    // headless test has no way to attach.
    private static NavItem Item(string label, string title, string subtitle) =>
        new(label, title, subtitle, null!, new StubPage(), _ => { });

    private static readonly IReadOnlyList<NavItem> Bar = [
        Item("Dashboard", "Dashboard", "Real-time system overview"),
        Item("Network", "Network", "Adapters, connections & diagnostics"),
        Item("Processes", "Processes", "Live processes & resource usage"),
    ];

    private static Task<IReadOnlyList<SearchResult>> Query(
        string term, IReadOnlyList<NavItem>? items = null, System.Action<NavItem>? navigate = null) =>
        new PageSearchProvider(items ?? Bar, navigate ?? (_ => { }))
            .QueryAsync(new SearchQuery(term), CancellationToken.None);

    [Fact]
    public async Task QueryAsync_FindsAPageByName() {
        var results = await Query("netw");

        Assert.Equal("Network", Assert.Single(results).Title);
    }

    [Fact]
    public async Task QueryAsync_FindsAPageByItsSubtitle() {
        var results = await Query("adapters");

        Assert.Equal("Network", Assert.Single(results).Title);
    }

    [Fact]
    public async Task QueryAsync_RanksTheNameAboveTheSubtitle() {
        // "processes" names one tab and appears in another's subtitle; the named tab must win.
        var results = await Query("processes");
        var ordered = results.OrderByDescending(r => r.Score).ToList();

        Assert.Equal("Processes", ordered[0].Title);
    }

    [Fact]
    public async Task QueryAsync_ActivatingAResultNavigatesToItsTab() {
        NavItem? navigated = null;
        var results = await Query("netw", navigate: item => navigated = item);

        Assert.Single(results).Activate();

        Assert.NotNull(navigated);
        Assert.Equal("Network", navigated.Title);
    }

    [Fact]
    public async Task QueryAsync_OffersThePageLabelAsTheCompletion() {
        var results = await Query("netw");

        Assert.Equal("Network", Assert.Single(results).Completion);
    }

    [Fact]
    public async Task QueryAsync_TagsEveryResultAsAPage() {
        var results = await Query("e");

        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.Equal(SearchCategory.Page, r.Category));
    }

    [Fact]
    public async Task QueryAsync_ReadsTheBarLiveSoALaterTabIsFound() {
        var bar = new List<NavItem>(Bar);
        var provider = new PageSearchProvider(bar, _ => { });

        bar.Add(Item("Storage", "Storage", "Drives, partitions & health"));
        var results = await provider.QueryAsync(new SearchQuery("storage"), CancellationToken.None);

        Assert.Equal("Storage", Assert.Single(results).Title);
    }

    private static NavItem Utilities() =>
        new("Utilities", "Utilities", "Common commands & diagnostics", null!, new StubPage(), _ => { },
            ["Toolkit", "Tools", "Commands"]);

    [Fact]
    public async Task QueryAsync_FindsARenamedPageByItsNewName() {
        var results = await Query("utilities", [Utilities()]);

        Assert.Equal("Utilities", Assert.Single(results).Title);
    }

    [Fact]
    public async Task QueryAsync_FindsARenamedPageByItsOldName() {
        var results = await Query("toolkit", [Utilities()]);

        var result = Assert.Single(results);
        Assert.Equal("Utilities", result.Title);
        Assert.Equal("Utilities", result.Completion);
    }

    [Fact]
    public async Task QueryAsync_RanksTheCurrentNameAboveAnAlias() {
        // "tools" is an alias of one page and the name of another; the named page must win.
        var bar = new List<NavItem>(Bar) { Item("Tools", "Tools", "Something else"), Utilities() };
        var ordered = (await Query("tools", bar)).OrderByDescending(r => r.Score).ToList();

        Assert.Equal("Tools", ordered[0].Title);
    }

    [Fact]
    public async Task QueryAsync_APageWithoutKeywordsStillMatchesNormally() {
        Assert.Equal("Network", Assert.Single(await Query("netw")).Title);
    }

    [Fact]
    public async Task QueryAsync_ReturnsNothingForATermNoTabMatches() {
        Assert.Empty(await Query("zzzz"));
    }
}
