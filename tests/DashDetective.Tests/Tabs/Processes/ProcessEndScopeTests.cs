using DashDetective.Tabs.Processes;
using System.Collections.Generic;
using Xunit;

namespace DashDetective.Tests.Tabs.Processes;

/// <summary>Pins what End task ends: a collapsed row stands for its whole subtree, an expanded one stands
/// only for itself, and a selected PID the tree never saw is still ended.</summary>
public class ProcessEndScopeTests {
    private static ProcessInfo Proc(int pid, int parentPid, string name) =>
        new(pid, parentPid, name, "Running", 0, 0, 1, ProcessCategory.App, 0, 0);

    /// <summary>An Edge-shaped group: 100 with children 101 and 102, and 102 with its own child 103.</summary>
    private static IReadOnlyList<ProcessNode> Tree() => ProcessTreeBuilder.Build([
        Proc(100, 0, "browser.exe"),
        Proc(101, 100, "browser.exe"),
        Proc(102, 100, "browser.exe"),
        Proc(103, 102, "browser.exe"),
        Proc(200, 0, "editor.exe"),
    ]);

    private static IReadOnlyList<int> Resolve(int[] selected, params int[] expanded) =>
        ProcessEndScope.Resolve(Tree(), new HashSet<int>(selected), new HashSet<int>(expanded));

    [Fact]
    public void Resolve_CollapsedRoot_TakesTheWholeSubtree() =>
        Assert.Equal([100, 101, 102, 103], Resolve([100]));

    [Fact]
    public void Resolve_ExpandedRoot_TakesOnlyItself() =>
        Assert.Equal([100], Resolve([100], 100));

    [Fact]
    public void Resolve_CollapsedChildOfAnExpandedParent_TakesItsOwnSubtree() =>
        Assert.Equal([100, 102, 103], Resolve([100, 102], 100));

    [Fact]
    public void Resolve_ChildSelectedWithoutItsParent_TakesOnlyThatBranch() =>
        Assert.Equal([102, 103], Resolve([102]));

    [Fact]
    public void Resolve_ParentAndChildBothSelected_ListsEachPidOnce() =>
        Assert.Equal([100, 101, 102, 103], Resolve([100, 101, 102]));

    [Fact]
    public void Resolve_ChildlessRow_TakesOnlyItself() =>
        Assert.Equal([200], Resolve([200]));

    [Fact]
    public void Resolve_SeveralRoots_ReadsInDisplayOrder() =>
        Assert.Equal([100, 101, 102, 103, 200], Resolve([100, 200]));

    /// <summary>A process the filter is hiding is still one the user picked, so it survives having no
    /// node in the tree the rows were built from.</summary>
    [Fact]
    public void Resolve_SelectedPidMissingFromTheTree_IsStillEnded() =>
        Assert.Equal([100, 101, 102, 103, 999], Resolve([100, 999]));

    [Fact]
    public void Resolve_NothingSelected_IsEmpty() => Assert.Empty(Resolve([]));

    /// <summary>A flat snapshot (no parents) behaves exactly as the selection did before the tree walk
    /// existed.</summary>
    [Fact]
    public void Resolve_FlatSnapshot_IsTheSelection() {
        var roots = ProcessTreeBuilder.Build([Proc(1, 0, "a.exe"), Proc(2, 0, "b.exe")]);

        Assert.Equal([1, 2], ProcessEndScope.Resolve(roots, new HashSet<int> { 1, 2 }, new HashSet<int>()));
    }
}
