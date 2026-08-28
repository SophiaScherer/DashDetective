using DashDetective.Shared.Layout;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace DashDetective.Tabs.Processes;

/// <summary>
/// The Processes table's column arithmetic. Every column is always shown: below the width at which the
/// weighted split still clears each column's minimum, the table scrolls sideways instead of dropping
/// columns, so no reading is ever taken off screen.
///
/// The header and the shared row template both size off <see cref="Definitions"/>, so they cannot
/// drift out of alignment. Metrics come from <see cref="ProcessColumns"/>.
/// </summary>
public static class ProcessTableLayout {
    /// <summary>Gap between columns, matching the table's ColumnSpacing.</summary>
    internal const double Spacing = 4;

    /// <summary>The table's ColumnDefinitions for a column order — the weights permuted to match it,
    /// so reordering the columns is a matter of what this string says and nothing else.</summary>
    public static string Definitions(IReadOnlyList<ProcessColumnId> order) {
        var builder = new StringBuilder();
        foreach (var id in order) {
            if (builder.Length > 0)
                builder.Append(',');
            builder.Append(ProcessColumns.WeightOf(id).ToString(CultureInfo.InvariantCulture)).Append('*');
        }

        return builder.ToString();
    }

    /// <summary>The narrowest the table may be laid out at: the width where the weighted split still
    /// meets every column's minimum, plus the gaps between them. Narrower than this and the table
    /// scrolls horizontally. Order-independent — the same columns are present either way. Shares
    /// <see cref="WeightedRowLayout.RequiredWidth"/> with the widget board, so one piece of arithmetic
    /// answers "does this weighted row still fit".</summary>
    public static double MinTableWidth { get; } = RequiredWidth();

    private static double RequiredWidth() {
        var order = ProcessColumns.DefaultOrder;
        var weights = new double[order.Count];
        var minimums = new double[order.Count];
        for (var i = 0; i < order.Count; i++) {
            weights[i] = ProcessColumns.WeightOf(order[i]);
            minimums[i] = ProcessColumns.MinWidthOf(order[i]);
        }

        return WeightedRowLayout.RequiredWidth(weights, minimums) + Spacing * (order.Count - 1);
    }
}
