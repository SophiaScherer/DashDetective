using DashDetective.Tabs.Processes;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace DashDetective.Tests.Tabs.Processes;

/// <summary>Covers the path search behind <c>ProcessesViewModel.Reveal</c>: jumping to a process nested
/// inside a collapsed multi-process app has to know which entries to expand on the way down, and must
/// leave nothing expanded when the process isn't there at all.</summary>
public class ProcessRevealPathTests {
    private static ProcessNode Node(int pid, params ProcessNode[] children) {
        var node = new ProcessNode(new ProcessInfo(
            pid, 0, "msedge.exe", "Running", 0, 0, 1, ProcessCategory.App, 0, 0));
        node.Children.AddRange(children);
        return node;
    }

    private static List<ProcessNode> Tree() => [
        Node(100, Node(110), Node(120, Node(121), Node(122))),
        Node(200),
    ];

    [Fact]
    public void TryFindPath_FindsARootAsAPathOfOne() {
        var path = new List<ProcessNode>();

        Assert.True(ProcessesViewModel.TryFindPath(Tree(), 200, path));
        Assert.Equal([200], path.Select(n => n.Info.Pid));
    }

    [Fact]
    public void TryFindPath_RecordsEveryAncestorOfANestedProcess() {
        var path = new List<ProcessNode>();

        Assert.True(ProcessesViewModel.TryFindPath(Tree(), 122, path));

        // Root → parent → target: everything but the last has to be expanded for the row to show.
        Assert.Equal([100, 120, 122], path.Select(n => n.Info.Pid));
    }

    [Fact]
    public void TryFindPath_LeavesThePathEmptyWhenThePidIsGone() {
        var path = new List<ProcessNode>();

        Assert.False(ProcessesViewModel.TryFindPath(Tree(), 999, path));
        Assert.Empty(path);
    }

    [Fact]
    public void TryFindPath_UnwindsBranchesItSearchedAndRejected() {
        // 121 sits under the second branch, so the first must not be left in the path behind it.
        var path = new List<ProcessNode>();

        Assert.True(ProcessesViewModel.TryFindPath(Tree(), 121, path));
        Assert.Equal([100, 120, 121], path.Select(n => n.Info.Pid));
    }

    [Fact]
    public void TryFindPath_HandlesAnEmptyTree() {
        var path = new List<ProcessNode>();

        Assert.False(ProcessesViewModel.TryFindPath([], 100, path));
        Assert.Empty(path);
    }
}
