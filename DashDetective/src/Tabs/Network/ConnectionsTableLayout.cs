using DashDetective.Shared.Layout;

namespace DashDetective.Tabs.Network;

/// <summary>
/// The Active Connections table's responsive column set. Protocol drops first, then State, then Remote
/// address; Process always stays. The minimums are for text at 100 %, so they are multiplied by the
/// text scale: at 200 % a column that fit "Established" no longer does, and the honest answer is a
/// column fewer rather than words run into each other.
///
/// Dropped columns stay in the definitions string at zero width, so every cell's Grid.Column is fixed
/// and the header cannot drift from the rows. All columns are stars (never Auto): the header and each
/// row are separate grids, and an Auto column would size to each one's own content.
/// </summary>
public static class ConnectionsTableLayout {
    /// <summary>Gap before every column but the first, applied as a cell margin so a dropped column
    /// reserves none.</summary>
    internal const double Spacing = 8;

    /// <summary>The font size, in px at 100 % text, that <see cref="ScaleOf"/> measures against: the
    /// header's Caption rung.</summary>
    internal const double ReferenceFontSize = 12;

    /// <summary>Minimum widths at 100 % text, in drop order: Process, Remote address, State, Protocol.</summary>
    internal static readonly double[] Minimums = { 96, 120, 80, 60 };

    private const int Required = 1;

    private const string All = "1.2*,1.4*,1*,0.7*";
    private const string WithoutProtocol = "1.2*,1.4*,1*,0*";
    private const string WithoutState = "1.2*,1.4*,0*,0*";
    private const string ProcessOnly = "1*,0*,0*,0*";

    /// <summary>The text scale a header at <paramref name="headerFontSize"/> implies, 1 at 100 %. An
    /// unusable size reads as 1 rather than hiding columns.</summary>
    public static double ScaleOf(double headerFontSize) =>
        double.IsFinite(headerFontSize) && headerFontSize > 0 ? headerFontSize / ReferenceFontSize : 1;

    /// <summary>Columns that fit <paramref name="width"/> at <paramref name="textScale"/>, 1 to 4.</summary>
    public static int VisibleCount(double width, double textScale) {
        var scale = double.IsFinite(textScale) && textScale > 0 ? textScale : 1;
        var scaled = new double[Minimums.Length];
        for (var i = 0; i < scaled.Length; i++)
            scaled[i] = Minimums[i] * scale;

        return TableColumns.VisibleColumns(width, scaled, Spacing, Required);
    }

    /// <summary>The ColumnDefinitions string. Always four columns; the dropped ones are zero-width.</summary>
    public static string Definitions(double width, double textScale) => VisibleCount(width, textScale) switch {
        >= 4 => All,
        3 => WithoutProtocol,
        2 => WithoutState,
        _ => ProcessOnly,
    };
}
