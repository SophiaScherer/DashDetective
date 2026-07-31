using DashDetective.Shared.Layout;

namespace DashDetective.Tabs.FileExplorer;

/// <summary>
/// The file list's responsive column set. Modified drops first, then Type; Name and Size always stay.
/// The list sits between two splitters, so it narrows both with the window and when the user drags a
/// pane — the same width rule covers both.
///
/// As with the Processes table, dropped columns stay in the definitions string at zero width so every
/// cell's Grid.Column index is fixed and the header cannot drift from the rows.
/// </summary>
public static class FileExplorerTableLayout {
    /// <summary>Gap between columns, matching the table's ColumnSpacing.</summary>
    internal const double Spacing = 8;

    /// <summary>Minimum widths in drop order: Name and Size, then Type, then Modified. This pane is
    /// narrow even at the design size (~291px for the header), so these sit just under the widths
    /// the columns already get there.</summary>
    internal static readonly double[] Minimums = { 105, 45, 48, 60 };

    private const int Required = 2;

    private const string All = "2.2*,1*,1.2*,0.9*";
    private const string WithoutModified = "2.2*,1*,0*,0.9*";
    private const string NameAndSizeOnly = "2.2*,0*,0*,0.9*";

    /// <summary>Columns that fit <paramref name="width"/>, between 2 and 4.</summary>
    public static int VisibleCount(double width) =>
        TableColumns.VisibleColumns(width, Minimums, Spacing, Required);

    /// <summary>Modified is last in drop order, so it needs all four columns to fit.</summary>
    public static bool ShowModified(double width) => VisibleCount(width) >= 4;

    public static bool ShowType(double width) => VisibleCount(width) >= 3;

    /// <summary>The ColumnDefinitions string for <paramref name="width"/>. Always four columns; the
    /// dropped ones are zero-width.</summary>
    public static string Definitions(double width) => VisibleCount(width) switch {
        >= 4 => All,
        3 => WithoutModified,
        _ => NameAndSizeOnly,
    };
}
