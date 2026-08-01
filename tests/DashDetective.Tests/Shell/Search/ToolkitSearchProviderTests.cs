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
        var results = await Query("appdata", entries: ToolkitCatalog.Entries);

        Assert.Contains(results, r => r.Title == "%appdata%");
    }
}
