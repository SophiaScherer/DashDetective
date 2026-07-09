using Avalonia.Media;

namespace DashDetective.Tabs.FileExplorer;

/// <summary>
/// Maps filesystem entries to the themed vector glyph + fixed colour used to draw them in the
/// tree, file list and details pane. Icons are drawn as stroked <see cref="Geometry"/> outlines
/// (no shell HICON→bitmap conversion). The per-type colours are semantic and fixed (like the
/// Palette's legend brushes), authored to match the design comp.
/// </summary>
public static class FileTypeCatalog {
    /// <summary>Folder outline with a raised left tab, authored in a 16x16 space.</summary>
    public static readonly Geometry FolderGlyph = Geometry.Parse(
        "M1.5,4 L6,4 L7.3,5.3 L14.5,5.3 L14.5,12.5 L1.5,12.5 Z");

    /// <summary>A page with a folded top-right corner, authored in a 16x16 space.</summary>
    public static readonly Geometry DocGlyph = Geometry.Parse(
        "M4,1.5 H9 L12.5,5 V14.5 H4 Z M9,1.5 V5 H12.5");

    /// <summary>Amber, matching the design comp's folder colour (#e8b64c).</summary>
    public static readonly IBrush FolderBrush = Brush("#e8b64c");

    // Semantic file-type colours from the comp (fixed, not theme-swapped).
    private static readonly IBrush Blue = Brush("#4cc2ff");
    private static readonly IBrush Green = Brush("#6ccb5f");
    private static readonly IBrush Purple = Brush("#c58fff");
    private static readonly IBrush Yellow = Brush("#ffcf4d");
    private static readonly IBrush Red = Brush("#ff6b6b");
    private static readonly IBrush Neutral = Brush("#9aa0a6");

    /// <summary>The glyph + colour to draw an entry with.</summary>
    public static (Geometry Glyph, IBrush Brush) ForEntry(bool isDirectory, string extension) =>
        isDirectory
            ? (FolderGlyph, FolderBrush)
            : (DocGlyph, BrushForExtension(extension));

    private static IBrush BrushForExtension(string extension) => extension.ToLowerInvariant() switch {
        ".doc" or ".docx" or ".rtf" or ".odt" => Blue,
        ".xls" or ".xlsx" or ".csv" => Green,
        ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".svg" or ".webp" => Green,
        ".pdf" => Red,
        ".zip" or ".rar" or ".7z" or ".tar" or ".gz" => Purple,
        ".json" or ".xml" or ".yml" or ".yaml" => Yellow,
        _ => Neutral,
    };

    private static IBrush Brush(string hex) => new SolidColorBrush(Color.Parse(hex));
}
