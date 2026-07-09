using Avalonia.Media;

namespace DashDetective.Tabs.FileExplorer;

/// <summary>
/// Maps filesystem entries to the themed vector glyph + fixed colour used to draw them in the
/// tree, file list and details pane. Icons are drawn as stroked <see cref="Geometry"/> outlines
/// (no shell HICON→bitmap conversion). Phase 2 covers folders; Phase 3 extends this with
/// per-extension file glyphs and colours.
/// </summary>
public static class FileTypeCatalog {
    /// <summary>Folder outline with a raised left tab, authored in a 16x16 space.</summary>
    public static readonly Geometry FolderGlyph = Geometry.Parse(
        "M1.5,4 L6,4 L7.3,5.3 L14.5,5.3 L14.5,12.5 L1.5,12.5 Z");

    /// <summary>Amber, matching the design comp's folder colour (#e8b64c).</summary>
    public static readonly IBrush FolderBrush = new SolidColorBrush(Color.Parse("#e8b64c"));
}
