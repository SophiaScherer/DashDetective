using System;
using System.Collections.Generic;

namespace DashDetective.Tabs.Processes;

/// <summary>
/// One node in the process tree: a process, its collapsed children, and the <see cref="Aggregate"/>
/// snapshot (its own metrics plus every descendant's). A childless node's <see cref="Aggregate"/> is
/// just its own <see cref="Info"/>. The view model renders roots collapsed (showing the aggregate) and
/// reveals <see cref="Children"/> on expand.
/// </summary>
public sealed class ProcessNode {
    public ProcessNode(ProcessInfo info) {
        Info = info;
        Aggregate = info;
    }

    public ProcessInfo Info { get; }
    public List<ProcessNode> Children { get; } = new();
    public ProcessInfo Aggregate { get; internal set; }
    public bool HasChildren => Children.Count > 0;
}

/// <summary>
/// Builds the Task-Manager-style process tree from a flat snapshot. A process nests under its parent
/// only when the parent is present in the snapshot and shares the same image name — so a multi-process
/// app's helpers (Edge's ~27 <c>msedge.exe</c>, Chrome, Electron apps, …) collapse under one entry,
/// while unrelated apps are <b>not</b> swallowed under whatever launched them (every app is a child of
/// <c>explorer.exe</c> by creation, but Task Manager doesn't nest them there, and neither do we). The
/// group takes the root process's category, so Edge's windowless helper processes fold into the Edge
/// <i>app</i> entry (the way Task Manager attributes them), not into Background. This same-executable
/// rule nails the dominant count inflation without the misgrouping a raw parent-PID tree would cause;
/// apps that spawn differently-named helpers are a documented divergence.
///
/// Pure and allocation-light; called each snapshot (and on re-sort) from the view model.
/// </summary>
public static class ProcessTreeBuilder {
    /// <summary>Returns the top-level nodes (group roots), each with its children attached and its
    /// <see cref="ProcessNode.Aggregate"/> computed.</summary>
    public static IReadOnlyList<ProcessNode> Build(IReadOnlyList<ProcessInfo> processes) {
        var nodes = new Dictionary<int, ProcessNode>(processes.Count);
        foreach (var info in processes)
            nodes[info.Pid] = new ProcessNode(info); // PIDs are unique among live processes

        var roots = new List<ProcessNode>();
        foreach (var node in nodes.Values) {
            if (TryFindParent(node, nodes, out var parent))
                parent.Children.Add(node);
            else
                roots.Add(node);
        }

        var visited = new HashSet<int>();
        foreach (var root in roots)
            ComputeAggregate(root, visited);

        return roots;
    }

    private static bool TryFindParent(ProcessNode node, Dictionary<int, ProcessNode> nodes, out ProcessNode parent) {
        parent = null!;
        var info = node.Info;
        if (info.ParentPid == 0 || info.ParentPid == info.Pid)
            return false;
        if (!nodes.TryGetValue(info.ParentPid, out var candidate) || ReferenceEquals(candidate, node))
            return false;
        if (!string.Equals(candidate.Info.Name, info.Name, StringComparison.OrdinalIgnoreCase))
            return false;
        if (CreatesCycle(node, candidate, nodes))
            return false;

        parent = candidate;
        return true;
    }

    /// <summary>Guards against PID reuse producing a parent link that loops back into the subtree
    /// (which would otherwise recurse forever): true if <paramref name="node"/> is already an ancestor
    /// of <paramref name="candidate"/>.</summary>
    private static bool CreatesCycle(ProcessNode node, ProcessNode candidate, Dictionary<int, ProcessNode> nodes) {
        var cursor = candidate.Info.ParentPid;
        for (var guard = 0; cursor != 0 && guard < 128; guard++) {
            if (cursor == node.Info.Pid)
                return true;
            if (!nodes.TryGetValue(cursor, out var up))
                break;
            cursor = up.Info.ParentPid;
        }
        return false;
    }

    /// <summary>Sums a node's own metrics with all descendants' into <see cref="ProcessNode.Aggregate"/>
    /// (CPU/memory/disk/GPU/threads), the way a collapsed Task Manager group shows totals. GPU is capped
    /// at 100%; CPU is already normalised to the 0–100 whole-machine scale, so its sum stays in range.</summary>
    private static ProcessInfo ComputeAggregate(ProcessNode node, HashSet<int> visited) {
        if (!visited.Add(node.Info.Pid) || node.Children.Count == 0) {
            node.Aggregate = node.Info;
            return node.Aggregate;
        }

        var own = node.Info;
        var cpu = own.CpuPercent;
        var memory = own.MemoryBytes;
        var threads = own.ThreadCount;
        var disk = own.DiskBytesPerSec;
        var gpu = own.GpuPercent;

        foreach (var child in node.Children) {
            var childAggregate = ComputeAggregate(child, visited);
            cpu += childAggregate.CpuPercent;
            memory += childAggregate.MemoryBytes;
            threads += childAggregate.ThreadCount;
            disk += childAggregate.DiskBytesPerSec;
            gpu += childAggregate.GpuPercent;
        }

        node.Aggregate = own with {
            CpuPercent = cpu,
            MemoryBytes = memory,
            ThreadCount = threads,
            DiskBytesPerSec = disk,
            GpuPercent = gpu > 100 ? 100 : gpu,
        };
        return node.Aggregate;
    }
}
