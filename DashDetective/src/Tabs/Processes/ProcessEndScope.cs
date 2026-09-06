using System.Collections.Generic;

namespace DashDetective.Tabs.Processes;

/// <summary>
/// Turns a selection of rows into the processes End task must actually end.
///
/// A collapsed row stands for its whole subtree — it shows the subtree's aggregate CPU and memory
/// (<see cref="ProcessTreeBuilder"/>) but carries only the root's PID, so ending the selection alone left
/// a multi-process app's helpers running and the row came back on the next poll. Descendants are taken
/// only for a <b>collapsed</b> node: an expanded one's children are rows of their own, which the user
/// selects or does not.
///
/// This is deliberately not <c>Process.Kill(entireProcessTree: true)</c>. The OS tree is not this tree —
/// nesting here needs a matching image name — so the OS call would end processes the row never claimed,
/// and would collapse the per-process outcomes into one boolean.
/// </summary>
internal static class ProcessEndScope {
    /// <summary>The PIDs to end, in display order and without duplicates. A selected PID with no node —
    /// hidden by the filter, or a flat snapshot — contributes just itself, so the existing "selection
    /// survives the filter" behaviour is unchanged.</summary>
    internal static IReadOnlyList<int> Resolve(
        IReadOnlyList<ProcessNode> roots, IReadOnlySet<int> selected, IReadOnlySet<int> expanded) {
        var order = new List<int>(selected.Count);
        var taken = new HashSet<int>();
        var found = new HashSet<int>();
        Walk(roots, selected, expanded, order, taken, found);

        // Anything selected that the tree never saw. Appended in selection order, after the rows.
        foreach (var pid in selected)
            if (!found.Contains(pid))
                Add(pid, order, taken);

        return order;
    }

    private static void Walk(IReadOnlyList<ProcessNode> nodes, IReadOnlySet<int> selected,
                             IReadOnlySet<int> expanded, List<int> order, HashSet<int> taken,
                             HashSet<int> found) {
        foreach (var node in nodes) {
            var pid = node.Info.Pid;
            found.Add(pid);

            if (selected.Contains(pid)) {
                Add(pid, order, taken);
                if (!expanded.Contains(pid))
                    AddSubtree(node.Children, order, taken);
            }

            // Descend regardless: a child can be selected under an unselected parent, and a collapsed
            // child under an expanded parent still takes its own subtree.
            Walk(node.Children, selected, expanded, order, taken, found);
        }
    }

    private static void AddSubtree(IReadOnlyList<ProcessNode> nodes, List<int> order, HashSet<int> taken) {
        foreach (var node in nodes) {
            Add(node.Info.Pid, order, taken);
            AddSubtree(node.Children, order, taken);
        }
    }

    private static void Add(int pid, List<int> order, HashSet<int> taken) {
        if (taken.Add(pid))
            order.Add(pid);
    }
}
