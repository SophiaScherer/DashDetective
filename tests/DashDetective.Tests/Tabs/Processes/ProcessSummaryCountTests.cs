using DashDetective.Tabs.Processes;
using System.Collections.Generic;
using Xunit;

namespace DashDetective.Tests.Tabs.Processes;

/// <summary>Covers the summary strip's process breakdown (<c>ProcessesViewModel.CountByGroup</c>). The strip
/// describes the machine, so it must count every live process — not the collapsed tree entries the list
/// shows — while still attributing each process to the group it appears under.</summary>
public class ProcessSummaryCountTests {
    private static ProcessInfo Info(int pid, string name, ProcessCategory category, int parentPid = 0) =>
        new(pid, parentPid, name, "Running", 0, 0, 1, category, 0, 0);

    /// <summary>An app with two collapsed helpers plus one standalone background process: four processes,
    /// but only two entries in the list.</summary>
    private static (List<ProcessInfo> Processes, IReadOnlyList<ProcessNode> Roots) Snapshot() {
        List<ProcessInfo> processes = [
            Info(100, "msedge.exe", ProcessCategory.App),
            Info(110, "msedge.exe", ProcessCategory.Background, parentPid: 100),
            Info(120, "msedge.exe", ProcessCategory.Background, parentPid: 100),
            Info(200, "svchost.exe", ProcessCategory.Windows),
        ];
        return (processes, ProcessTreeBuilder.Build(processes));
    }

    [Fact]
    public void CountByGroup_CountsEveryProcess_NotTheCollapsedEntries() {
        var (processes, roots) = Snapshot();

        var (apps, background, windows) = ProcessesViewModel.CountByGroup(processes, roots);

        // Two list entries (msedge + svchost), but four real processes.
        Assert.Equal(2, roots.Count);
        Assert.Equal(processes.Count, apps + background + windows);
    }

    /// <summary>A helper is counted in the group its root is displayed under, so Edge's windowless children
    /// land in Apps beside Edge rather than in Background on their own.</summary>
    [Fact]
    public void CountByGroup_AttributesHelpersToTheirRootsGroup() {
        var (processes, roots) = Snapshot();

        var (apps, background, windows) = ProcessesViewModel.CountByGroup(processes, roots);

        Assert.Equal(3, apps);
        Assert.Equal(0, background);
        Assert.Equal(1, windows);
    }

    /// <summary>A process the tree didn't reach still lands in a group, so the breakdown can never sum to
    /// less than the total.</summary>
    [Fact]
    public void CountByGroup_UnreachableProcess_FallsBackToItsOwnCategory() {
        List<ProcessInfo> processes = [Info(300, "orphan.exe", ProcessCategory.Windows)];

        var (apps, background, windows) = ProcessesViewModel.CountByGroup(processes, []);

        Assert.Equal(0, apps);
        Assert.Equal(0, background);
        Assert.Equal(1, windows);
    }

    [Fact]
    public void CountByGroup_EmptySnapshot_CountsNothing() {
        Assert.Equal((0, 0, 0), ProcessesViewModel.CountByGroup([], []));
    }
}
