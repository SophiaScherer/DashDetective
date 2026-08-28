using DashDetective.Shared.Layout;
using System.Globalization;
using System.Linq;

namespace DashDetective.Tabs.Processes;

/// <summary>
/// The Processes table's column metrics. Every column is always shown: below the width at which the
/// weighted split still clears each column's minimum, the table scrolls sideways instead of dropping
/// columns, so no reading is ever taken off screen.
///
/// The header and the shared row template both size off <see cref="Definitions"/>, so they cannot
/// drift out of alignment.
/// </summary>
public static class ProcessTableLayout {
    /// <summary>Gap between columns, matching the table's ColumnSpacing.</summary>
    internal const double Spacing = 4;

    /// <summary>Each column's narrowest readable width, in table order: Name, PID, Status, CPU,
    /// Memory, Disk, GPU.</summary>
    internal static readonly double[] Minimums = { 180, 52, 58, 72, 68, 66, 56 };

    /// <summary>Each column's share of the table, in the same order as <see cref="Minimums"/>.</summary>
    internal static readonly double[] Weights = { 2.4, 0.7, 1, 0.85, 0.85, 0.85, 0.85 };

    /// <summary>The table's ColumnDefinitions string.</summary>
    public static string Definitions { get; } = string.Join(",", Weights.Select(FormatWeight));

    /// <summary>The narrowest the table may be laid out at: the width where the weighted split still
    /// meets every column's minimum, plus the gaps between them. Narrower than this and the table
    /// scrolls horizontally. Shares <see cref="WeightedRowLayout.RequiredWidth"/> with the widget
    /// board, so one piece of arithmetic answers "does this weighted row still fit".</summary>
    public static double MinTableWidth { get; } =
        WeightedRowLayout.RequiredWidth(Weights, Minimums) + Spacing * (Weights.Length - 1);

    private static string FormatWeight(double weight) =>
        weight.ToString(CultureInfo.InvariantCulture) + "*";
}
