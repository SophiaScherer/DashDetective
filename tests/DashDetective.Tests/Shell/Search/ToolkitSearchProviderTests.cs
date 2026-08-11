using DashDetective.Shell.Search;
using DashDetective.Shell.Search.Providers;
using DashDetective.Tabs.Toolkit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DashDetective.Tests.Shell.Search;

/// <summary>Covers <see cref="ToolkitSearchProvider"/>: a command is reachable by its own text or by
/// what it does, the command outranks the description, picking a result asks for that command, and an
/// empty command set is answered with nothing rather than an error — which is the shipped state until
/// the entries are authored.</summary>
public class ToolkitSearchProviderTests {
    private static readonly IReadOnlyList<ToolkitEntry> Sample = [
        new("ipconfig /flushdns", "Clears the DNS resolver cache",
            ToolkitCategory.Diagnostics, ToolkitEntryKind.Command,
            ToolkitAction.Capture("ipconfig", "/flushdns")),
        new("ncpa.cpl", "Opens the network adapter list",
            ToolkitCategory.SystemTools, ToolkitEntryKind.Panel,
            ToolkitAction.Launch("ncpa.cpl")),
        new("shell:startup", "Opens the folder ipconfig knows nothing about",
            ToolkitCategory.Folders, ToolkitEntryKind.Folder,
            ToolkitAction.OpenPath("shell:startup")),
    ];

    private static Task<IReadOnlyList<SearchResult>> Query(
        string term, IReadOnlyList<ToolkitEntry>? entries = null, Action<string>? reveal = null) =>
        new ToolkitSearchProvider(() => entries ?? Sample, reveal ?? (_ => { }))
            .QueryAsync(new SearchQuery(term), CancellationToken.None);

    private static async Task<SearchResult> Best(string term) =>
        (await Query(term)).OrderByDescending(r => r.Score).First();

    [Fact]
    public async Task QueryAsync_FindsACommandByItsText() {
        Assert.Equal("ncpa.cpl", (await Best("ncpa")).Title);
    }

    [Fact]
    public async Task QueryAsync_FindsACommandByWhatItDoes() {
        // "resolver" appears only in the description, which is the point of searching it.
        Assert.Equal("ipconfig /flushdns", (await Best("resolver cache")).Title);
    }

    [Fact]
    public async Task QueryAsync_RanksTheCommandAboveTheDescription() {
        // "ipconfig" is one command's text and another's description; the command must win.
        Assert.Equal("ipconfig /flushdns", (await Best("ipconfig")).Title);
    }

    [Fact]
    public async Task QueryAsync_ActivatingAResultAsksForThatCommand() {
        string? revealed = null;
        var results = await Query("ncpa", reveal: c => revealed = c);

        results.OrderByDescending(r => r.Score).First().Activate();

        Assert.Equal("ncpa.cpl", revealed);
    }

    [Fact]
    public async Task QueryAsync_DescribesEachResultByWhatTheCommandDoes() {
        Assert.Equal("Opens the network adapter list", (await Best("ncpa")).Subtitle);
    }

    [Fact]
    public async Task QueryAsync_IdentifiesAResultByItsCommand() {
        Assert.Equal("ncpa.cpl", (await Best("ncpa")).Identity);
    }

    [Fact]
    public async Task QueryAsync_TagsEveryResultAsAToolkitCommand() {
        var results = await Query("o");

        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.Equal(SearchCategory.Toolkit, r.Category));
    }

    [Fact]
    public async Task QueryAsync_ReturnsNothingForATermNoCommandMatches() {
        Assert.Empty(await Query("zzzz"));
    }

    /// <summary>A filtered-to-nothing command set must simply find nothing rather than fault the whole
    /// query — the provider reads the catalog through a callback, so it cannot assume a populated one.</summary>
    [Fact]
    public async Task QueryAsync_EmptyCommandSet_ReturnsNothing() {
        Assert.Empty(await Query("ipconfig", entries: []));
    }

    /// <summary>The real catalog is reachable through the provider, so an authored command is findable
    /// from the toolbar without the provider being told about it.</summary>
    [Fact]
    public async Task QueryAsync_RealCatalog_FindsAnAuthoredCommand() {
        var results = await Query("appdata", entries: WindowsToolkitCatalog.Instance.Entries);

        Assert.Contains(results, r => r.Title == "%appdata%");
    }

    /// <summary>The page's merged list, not the catalog, is what the shell hands the provider — so a
    /// command the user authored is findable from the toolbar exactly like an authored one.</summary>
    [Fact]
    public async Task QueryAsync_PageEntries_FindTheUsersOwnCommandsToo() {
        var page = new ToolkitViewModel(WindowsToolkitCatalog.Instance);
        page.AddCommand(new ToolkitCommand(
            "zzz-my-own", "Something only I have", ToolkitCommandType.Launch, "thing.exe"));

        var byTitle = await Query("zzz-my-own", entries: page.AllEntries);
        var byDescription = await Query("only I have", entries: page.AllEntries);
        var authored = await Query("appdata", entries: page.AllEntries);

        Assert.Contains(byTitle, r => r.Title == "zzz-my-own");
        Assert.Contains(byDescription, r => r.Title == "zzz-my-own");
        Assert.Contains(authored, r => r.Title == "%appdata%");
    }

    /// <summary>Read through a callback at query time, so a command added after the provider was built is
    /// findable without anything having to re-register it.</summary>
    [Fact]
    public async Task QueryAsync_ACommandAddedAfterwards_IsFoundWithoutRewiring() {
        var page = new ToolkitViewModel(WindowsToolkitCatalog.Instance);
        var provider = new ToolkitSearchProvider(() => page.AllEntries, _ => { });

        Assert.Empty(await provider.QueryAsync(new SearchQuery("zzz-later"), CancellationToken.None));

        page.AddCommand(new ToolkitCommand("zzz-later", "", ToolkitCommandType.Launch, "thing.exe"));

        Assert.Contains(
            await provider.QueryAsync(new SearchQuery("zzz-later"), CancellationToken.None),
            r => r.Title == "zzz-later");
    }
}
