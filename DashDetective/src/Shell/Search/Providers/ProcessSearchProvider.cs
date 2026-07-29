using Avalonia.Media;
using DashDetective.Tabs.Processes;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace DashDetective.Shell.Search.Providers;

/// <summary>
/// Finds running processes by name or PID, over the snapshot the Processes tab already polls — searching
/// costs no extra enumeration, and the list matched is the one the user would be looking at.
///
/// A multi-process app appears in that snapshot once per process (Edge alone is dozens of identical
/// <c>msedge.exe</c> rows), so results are folded by name: one row per program, captioned with how many
/// processes it is running. Picking it filters the tab to that program and selects its entry.
/// </summary>
public sealed class ProcessSearchProvider : ISearchProvider {
    private readonly Func<IReadOnlyList<ProcessInfo>> _snapshot;
    private readonly Action<int> _reveal;
    private readonly Geometry? _icon;

    /// <param name="snapshot">Reads the Processes tab's latest poll. A delegate rather than a list so
    /// each query sees the current one.</param>
    /// <param name="reveal">Navigates to Processes and reveals the process with this PID.</param>
    /// <param name="icon">The row glyph (see <see cref="SettingSearchProvider"/> on why it's injected).</param>
    public ProcessSearchProvider(
        Func<IReadOnlyList<ProcessInfo>> snapshot, Action<int> reveal, Geometry? icon = null) {
        _snapshot = snapshot;
        _reveal = reveal;
        _icon = icon;
    }

    public SearchCategory Category => SearchCategory.Process;

    public Task<IReadOnlyList<SearchResult>> QueryAsync(SearchQuery query, CancellationToken token) {
        var term = query.Term;

        // Best match per program name: the lowest PID stands for the group, which is the process the
        // others were spawned from and so the one the tab's tree collapses them under.
        var groups = new Dictionary<string, Group>(StringComparer.OrdinalIgnoreCase);

        foreach (var process in _snapshot()) {
            // The tab's own filter rule, so a term that narrows the list there finds the same processes
            // here — including the PID-prefix match, which no name comparison would catch.
            if (!ProcessFilter.Matches(process.Name, process.Pid, term))
                continue;

            var score = SearchRanker.ScoreBest(term, process.Name, PidOf(process));
            if (score == SearchRanker.NoMatch)
                continue;

            if (groups.TryGetValue(process.Name, out var group))
                groups[process.Name] = group with {
                    Count = group.Count + 1,
                    Pid = Math.Min(group.Pid, process.Pid),
                    Score = Math.Max(group.Score, score),
                };
            else
                groups[process.Name] = new Group(process.Pid, 1, score);
        }

        var results = new List<SearchResult>(groups.Count);
        foreach (var (name, group) in groups) {
            var pid = group.Pid;
            results.Add(new SearchResult(
                SearchCategory.Process, name, Caption(group), group.Score,
                () => _reveal(pid), _icon, name));
        }

        return Task.FromResult<IReadOnlyList<SearchResult>>(results);
    }

    /// <summary>One program's processes: the entry PID, how many there are, and the best score any of
    /// them matched at.</summary>
    private readonly record struct Group(int Pid, int Count, int Score);

    // "PID 4812" for a lone process; "PID 4812 · 27 processes" for a program running a fleet of them.
    private static string Caption(Group group) {
        var pid = "PID " + group.Pid.ToString(CultureInfo.InvariantCulture);
        return group.Count == 1
            ? pid
            : $"{pid} · {group.Count.ToString(CultureInfo.InvariantCulture)} processes";
    }

    private static string PidOf(ProcessInfo process) =>
        process.Pid.ToString(CultureInfo.InvariantCulture);
}
