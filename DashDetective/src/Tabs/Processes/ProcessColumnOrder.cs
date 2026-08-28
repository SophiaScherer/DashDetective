using DashDetective.Shared.Layout;
using System;
using System.Collections.Generic;
using System.Text;

namespace DashDetective.Tabs.Processes;

/// <summary>
/// Encodes the user's column order as one flat string, so <c>AppSettings</c> needs no knowledge of what
/// a process column is — the encoding lives next to the thing it encodes, as <c>WidgetOrders</c> and
/// <c>ToolkitPins</c> do.
///
/// Order is stored by column name, never by index: a table that gains or loses a column between
/// releases must not silently re-point a saved order at different columns.
/// </summary>
public static class ProcessColumnOrder {
    // An ASCII unit separator. A control character, so it cannot occur in a column name, which is what
    // makes joining without escaping safe.
    private const char Separator = (char)0x1F;

    /// <summary>The order as one persistable string.</summary>
    public static string Encode(IReadOnlyList<ProcessColumnId> order) {
        var builder = new StringBuilder();
        var seen = new HashSet<ProcessColumnId>();
        foreach (var id in order) {
            if (!seen.Add(id))
                continue;
            if (builder.Length > 0)
                builder.Append(Separator);
            builder.Append(id.ToString());
        }

        return builder.ToString();
    }

    /// <summary>The saved order read back. Total: a name no column answers to — a hand-edit, or a
    /// column since removed — is dropped rather than thrown on.</summary>
    public static IReadOnlyList<ProcessColumnId> Decode(string? encoded) {
        var order = new List<ProcessColumnId>(ProcessColumns.Count);
        if (string.IsNullOrEmpty(encoded))
            return order;

        var seen = new HashSet<ProcessColumnId>();
        foreach (var field in encoded.Split(Separator))
            if (Enum.TryParse<ProcessColumnId>(field, out var id) && Enum.IsDefined(id) && seen.Add(id))
                order.Add(id);

        return order;
    }

    /// <summary>The order to actually show: <paramref name="saved"/> reconciled against the columns the
    /// table declares now (via the shared <see cref="OrderResolver"/>), with the pinned column forced
    /// back to the front however the save was written.</summary>
    public static IReadOnlyList<ProcessColumnId> Resolve(IReadOnlyList<ProcessColumnId> saved) {
        var declaredNames = Names(ProcessColumns.DefaultOrder);
        var resolvedNames = OrderResolver.Resolve(declaredNames, Names(saved));

        var resolved = new List<ProcessColumnId>(resolvedNames.Count);
        foreach (var name in resolvedNames)
            resolved.Add(Enum.Parse<ProcessColumnId>(name));

        var pinned = resolved.IndexOf(ProcessColumns.Pinned);
        if (pinned > 0) {
            resolved.RemoveAt(pinned);
            resolved.Insert(0, ProcessColumns.Pinned);
        }

        return resolved;
    }

    private static List<string> Names(IReadOnlyList<ProcessColumnId> order) {
        var names = new List<string>(order.Count);
        foreach (var id in order)
            names.Add(id.ToString());
        return names;
    }
}
