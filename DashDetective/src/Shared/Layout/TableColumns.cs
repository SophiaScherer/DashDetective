using System;

namespace DashDetective.Shared.Layout;

/// <summary>
/// How many of a table's columns survive at a given width. A star-sized table keeps shrinking its
/// columns until every cell is unreadable, so past a point the honest answer is to show fewer
/// columns rather than narrower ones.
/// </summary>
public static class TableColumns {
    /// <summary>The count of columns that fit <paramref name="availableWidth"/>. Columns are supplied
    /// in <b>drop order</b> — the ones that must stay first, the first to go last — and are dropped
    /// from the end until the remainder fits. Never fewer than <paramref name="required"/>, and an
    /// unconstrained width keeps them all.</summary>
    public static int VisibleColumns(double availableWidth, ReadOnlySpan<double> minWidths,
                                     double spacing, int required = 1) {
        var count = minWidths.Length;
        var floor = Math.Clamp(required, 1, Math.Max(1, count));
        if (count <= floor || !double.IsFinite(availableWidth))
            return count;

        var total = 0.0;
        foreach (var min in minWidths)
            total += min;

        while (count > floor && total + spacing * (count - 1) > availableWidth)
            total -= minWidths[--count];

        return count;
    }
}
