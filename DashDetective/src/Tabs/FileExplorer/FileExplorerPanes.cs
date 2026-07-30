namespace DashDetective.Tabs.FileExplorer;

/// <summary>
/// Which of the File Explorer's three panes fit at a given page width. The panes have hard minimums
/// (tree 180, list 320, details 220, plus two 12px splitters), so without this the grid is
/// over-constrained on a narrow window and simply clips off the right edge instead of adapting.
/// The details pane goes first, then the folder tree, leaving the file list — the point of the page.
/// </summary>
public static class FileExplorerPanes {
    internal const double TreeWidth = 240;
    internal const double DetailsWidth = 300;
    internal const double SplitterWidth = 12;

    // Tree min + splitter + list min, and the same again plus the details min.
    internal const double TreeThreshold = 180 + SplitterWidth + 320;
    internal const double DetailsThreshold = TreeThreshold + SplitterWidth + 220;

    /// <summary>Whether the folder tree fits alongside the file list.</summary>
    public static bool ShowTree(double pageWidth) => pageWidth >= TreeThreshold;

    /// <summary>Whether the details pane fits as well. Always false when the tree does not fit, so the
    /// panes disappear in a fixed order rather than swapping.</summary>
    public static bool ShowDetails(double pageWidth) => pageWidth >= DetailsThreshold;
}
