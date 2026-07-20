using DashDetective.Tabs.Processes;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace DashDetective.Tests.Tabs.Processes;

/// <summary>Covers <see cref="ProcessTreeBuilder.Build"/>: same-executable collapse, orphan handling,
/// self/cross-name and cycle guards, and the aggregate roll-up (sum with GPU capped at 100). Root
/// ordering is not asserted — it follows dictionary enumeration.</summary>
public class ProcessTreeBuilderTests {
    private static ProcessInfo Proc(int pid, int parentPid, string name,
        double cpu = 0, long mem = 0, int threads = 0, double disk = 0, double gpu = 0) =>
        new(pid, parentPid, name, "Running", cpu, mem, threads, ProcessCategory.Background, disk, gpu);

    private static ProcessNode Root(IReadOnlyList<ProcessNode> roots, int pid) =>
        roots.Single(r => r.Info.Pid == pid);

    [Fact]
    public void Build_SameNameChildren_CollapseUnderOneRoot() {
        var roots = ProcessTreeBuilder.Build(new[] {
            Proc(1, 0, "app.exe"),
            Proc(2, 1, "app.exe"),
            Proc(3, 1, "app.exe"),
        });

        var root = Assert.Single(roots);
        Assert.True(root.HasChildren);
        Assert.Equal(2, root.Children.Count);
    }

    [Fact]
    public void Build_DifferentlyNamedChild_IsNotNestedUnderLauncher() {
        // Every app is a child of explorer.exe by creation, but a different image name is not nested.
        var roots = ProcessTreeBuilder.Build(new[] {
            Proc(1, 0, "explorer.exe"),
            Proc(2, 1, "app.exe"),
        });

        Assert.Equal(2, roots.Count);
        Assert.False(Root(roots, 1).HasChildren);
    }

    [Fact]
    public void Build_AbsentParent_BecomesRoot() {
        var roots = ProcessTreeBuilder.Build(new[] { Proc(2, 999, "app.exe") });
        var root = Assert.Single(roots);
        Assert.Equal(2, root.Info.Pid);
    }

    [Fact]
    public void Build_ZeroParentPid_BecomesRoot() {
        var roots = ProcessTreeBuilder.Build(new[] { Proc(5, 0, "svc.exe") });
        Assert.Equal(5, Assert.Single(roots).Info.Pid);
    }

    [Fact]
    public void Build_SelfParent_BecomesRootNotNested() {
        var roots = ProcessTreeBuilder.Build(new[] { Proc(7, 7, "x.exe") });
        var root = Assert.Single(roots);
        Assert.False(root.HasChildren);
    }

    [Fact]
    public void Build_MutualParentCycle_TerminatesWithBothAsRoots() {
        // PID reuse can make two same-name processes point at each other; the cycle guard must not hang.
        var roots = ProcessTreeBuilder.Build(new[] {
            Proc(1, 2, "a.exe"),
            Proc(2, 1, "a.exe"),
        });

        Assert.Equal(2, roots.Count);
    }

    [Fact]
    public void Build_Aggregate_SumsOwnPlusDescendants() {
        var roots = ProcessTreeBuilder.Build(new[] {
            Proc(1, 0, "app.exe", cpu: 10, mem: 100, threads: 2, disk: 5, gpu: 20),
            Proc(2, 1, "app.exe", cpu: 5, mem: 50, threads: 3, disk: 2, gpu: 30),
        });

        var root = Root(roots, 1);
        Assert.Equal(15, root.Aggregate.CpuPercent);
        Assert.Equal(150, root.Aggregate.MemoryBytes);
        Assert.Equal(5, root.Aggregate.ThreadCount);
        Assert.Equal(7, root.Aggregate.DiskBytesPerSec);
        Assert.Equal(50, root.Aggregate.GpuPercent);
        Assert.Equal(10, root.Info.CpuPercent);   // Info is left untouched
    }

    [Fact]
    public void Build_ChildlessNode_AggregateEqualsInfo() {
        var roots = ProcessTreeBuilder.Build(new[] { Proc(1, 0, "solo.exe", cpu: 4, gpu: 8) });
        var root = Assert.Single(roots);
        Assert.Same(root.Info, root.Aggregate);
    }

    [Fact]
    public void Build_Aggregate_CapsGpuAtHundredButNotCpu() {
        var roots = ProcessTreeBuilder.Build(new[] {
            Proc(1, 0, "app.exe", cpu: 60, gpu: 60),
            Proc(2, 1, "app.exe", cpu: 70, gpu: 70),
        });

        var root = Root(roots, 1);
        Assert.Equal(100, root.Aggregate.GpuPercent);   // 130 capped
        Assert.Equal(130, root.Aggregate.CpuPercent);   // not capped
    }
}
