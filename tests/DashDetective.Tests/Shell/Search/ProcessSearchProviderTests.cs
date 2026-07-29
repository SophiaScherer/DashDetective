using DashDetective.Shell.Search;
using DashDetective.Shell.Search.Providers;
using DashDetective.Tabs.Processes;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DashDetective.Tests.Shell.Search;

/// <summary>Covers <see cref="ProcessSearchProvider"/>: a multi-process app folds into one result rather
/// than flooding the list, PIDs are matched by prefix as the Processes filter does, and picking a result
/// reveals the entry process the tab collapses the group under.</summary>
public class ProcessSearchProviderTests {
    private static ProcessInfo Process(int pid, string name) =>
        new(pid, 0, name, "Running", 0, 0, 1, ProcessCategory.App, 0, 0);

    private static readonly IReadOnlyList<ProcessInfo> Snapshot = [
        Process(4812, "msedge.exe"),
        Process(5100, "msedge.exe"),
        Process(5104, "msedge.exe"),
        Process(900, "explorer.exe"),
        Process(1204, "notepad.exe"),
    ];

    private static Task<IReadOnlyList<SearchResult>> Query(
        string term, IReadOnlyList<ProcessInfo>? snapshot = null, System.Action<int>? reveal = null) =>
        new ProcessSearchProvider(() => snapshot ?? Snapshot, reveal ?? (_ => { }))
            .QueryAsync(new SearchQuery(term), CancellationToken.None);

    [Fact]
    public async Task QueryAsync_FoldsAMultiProcessAppIntoOneResult() {
        var results = await Query("msedge");

        var result = Assert.Single(results);
        Assert.Equal("msedge.exe", result.Title);
        Assert.Equal("PID 4812 · 3 processes", result.Subtitle);
    }

    [Fact]
    public async Task QueryAsync_CaptionsALoneProcessWithJustItsPid() {
        Assert.Equal("PID 1204", Assert.Single(await Query("notepad")).Subtitle);
    }

    [Fact]
    public async Task QueryAsync_MatchesAPidByItsPrefix() {
        // The Processes filter narrows on a partly-typed PID; search must find the same thing.
        var results = await Query("120");

        Assert.Equal("notepad.exe", Assert.Single(results).Title);
    }

    [Fact]
    public async Task QueryAsync_RevealsTheEntryProcessOfAGroup() {
        // The tab collapses a group under its lowest PID, so that is the one to jump to — not whichever
        // helper the snapshot happened to list first.
        int? revealed = null;
        var results = await Query("msedge", reveal: pid => revealed = pid);

        results[0].Activate();

        Assert.Equal(4812, revealed);
    }

    [Fact]
    public async Task QueryAsync_ReadsTheSnapshotFreshOnEveryQuery() {
        var snapshot = new List<ProcessInfo>(Snapshot);
        var provider = new ProcessSearchProvider(() => snapshot, _ => { });

        snapshot.Add(Process(7000, "newapp.exe"));
        var results = await provider.QueryAsync(new SearchQuery("newapp"), CancellationToken.None);

        Assert.Equal("newapp.exe", Assert.Single(results).Title);
    }

    [Fact]
    public async Task QueryAsync_IgnoresCase() {
        Assert.Equal("msedge.exe", (await Query("MSEDGE"))[0].Title);
    }

    [Fact]
    public async Task QueryAsync_TagsEveryResultAsAProcess() {
        var results = await Query("exe");

        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.Equal(SearchCategory.Process, r.Category));
    }

    [Fact]
    public async Task QueryAsync_OffersTheProcessNameAsTheCompletion() {
        Assert.Equal("notepad.exe", Assert.Single(await Query("notepad")).Completion);
    }

    [Fact]
    public async Task QueryAsync_ReturnsNothingForATermNoProcessMatches() {
        Assert.Empty(await Query("zzzz"));
    }

    [Fact]
    public async Task QueryAsync_ReturnsNothingBeforeTheFirstPoll() {
        Assert.Empty(await Query("msedge", snapshot: []));
    }
}
