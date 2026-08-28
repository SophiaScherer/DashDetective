using System;
using System.Collections.Generic;

namespace DashDetective.Tabs.Processes;

/// <summary>
/// The Processes table's columns: their declared order and per-column metrics. One table, so the
/// layout arithmetic and the saved column order cannot disagree about what a column is or how wide it
/// needs to be.
/// </summary>
public static class ProcessColumns {
    /// <summary>Name owns the tree indent, the expand chevron and the status dot, so it stays leftmost
    /// and cannot be dragged — a hierarchy indented from the middle of the table is unreadable.</summary>
    public const ProcessColumnId Pinned = ProcessColumnId.Name;

    /// <summary>Each column's narrowest readable width and its share of the table. Indexed by
    /// <see cref="ProcessColumnId"/>, so this array's order must match the enum's.</summary>
    private static readonly (ProcessColumnId Id, double MinWidth, double Weight)[] Table = {
        (ProcessColumnId.Name, 180, 2.4),
        (ProcessColumnId.Pid, 52, 0.7),
        (ProcessColumnId.Status, 58, 1),
        (ProcessColumnId.Cpu, 72, 0.85),
        (ProcessColumnId.Memory, 68, 0.85),
        (ProcessColumnId.Disk, 66, 0.85),
        (ProcessColumnId.Gpu, 56, 0.85),
    };

    /// <summary>The order the table ships in, and what a saved order is resolved against.</summary>
    public static IReadOnlyList<ProcessColumnId> DefaultOrder { get; } =
        Array.ConvertAll(Table, entry => entry.Id);

    public static int Count => Table.Length;

    /// <summary>The narrowest width this column stays readable at.</summary>
    public static double MinWidthOf(ProcessColumnId id) => Table[(int)id].MinWidth;

    /// <summary>This column's share of the table's width.</summary>
    public static double WeightOf(ProcessColumnId id) => Table[(int)id].Weight;
}
