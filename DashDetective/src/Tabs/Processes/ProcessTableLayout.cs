using DashDetective.Shared.Layout;

namespace DashDetective.Tabs.Processes;

/// <summary>
/// The Processes table's responsive column set. Columns drop as the panel narrows — GPU first, then
/// Disk, then Status — while Name, PID, CPU and Memory always stay.
///
/// Hidden columns keep their place in the definitions string at zero width rather than being removed,
/// so every cell's Grid.Column index stays fixed and the header and the row template cannot drift
/// out of alignment.
/// </summary>
public static class ProcessTableLayout {
    /// <summary>Gap between columns, matching the table's ColumnSpacing.</summary>
    internal const double Spacing = 4;

    /// <summary>Minimum widths in drop order: the four that always stay, then Status, Disk, GPU.
    /// Each is below the width that column already gets at the 1180px design size, so no column
    /// disappears at a width where it used to be perfectly readable.</summary>
    internal static readonly double[] Minimums = { 180, 52, 58, 72, 68, 66, 56 };

    private const int Required = 4;

    private const string All = "2.4*,0.7*,1*,0.85*,0.85*,0.85*,0.85*";
    private const string NoGpu = "2.4*,0.7*,1*,0.85*,0.85*,0.85*,0*";
    private const string NoDisk = "2.4*,0.7*,1*,0.85*,0.85*,0*,0*";
    private const string NoStatus = "2.4*,0.7*,0*,0.85*,0.85*,0*,0*";

    /// <summary>Columns that fit <paramref name="width"/>, between 4 and 7.</summary>
    public static int VisibleCount(double width) =>
        TableColumns.VisibleColumns(width, Minimums, Spacing, Required);

    public static bool ShowStatus(double width) => VisibleCount(width) >= 5;

    public static bool ShowDisk(double width) => VisibleCount(width) >= 6;

    public static bool ShowGpu(double width) => VisibleCount(width) >= 7;

    /// <summary>The ColumnDefinitions string for <paramref name="width"/>. Always seven columns; the
    /// dropped ones are zero-width. The header and the shared row template both use this, so they
    /// stay aligned by construction.</summary>
    public static string Definitions(double width) => VisibleCount(width) switch {
        >= 7 => All,
        6 => NoGpu,
        5 => NoDisk,
        _ => NoStatus,
    };
}
