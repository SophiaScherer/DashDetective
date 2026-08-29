using Avalonia.Media;
using DashDetective.Services.Theming;

namespace DashDetective.Tabs.FileExplorer;

/// <summary>
/// Maps filesystem entries to the themed vector glyph + fixed colour used to draw them in the
/// tree, file list and details pane. Icons are drawn as stroked <see cref="Geometry"/> outlines
/// (no shell HICON→bitmap conversion). The per-type colours are the palette's semantic, fixed
/// brushes (see <see cref="SemanticBrushes"/>), so they never drift from the rest of the app.
/// </summary>
public static class FileTypeCatalog {
    /// <summary>Folder outline with a raised left tab, authored in a 16x16 space.</summary>
    public static readonly Geometry FolderGlyph = Geometry.Parse(
        "M1.5,4 L6,4 L7.3,5.3 L14.5,5.3 L14.5,12.5 L1.5,12.5 Z");

    /// <summary>A page with a folded top-right corner, authored in a 16x16 space.</summary>
    public static readonly Geometry DocGlyph = Geometry.Parse(
        "M4,1.5 H9 L12.5,5 V14.5 H4 Z M9,1.5 V5 H12.5");

    /// <summary>Amber. The palette's yellow, which the comp's folder colour was a near-duplicate of.</summary>
    public static readonly IBrush FolderBrush = SemanticBrushes.Yellow;

    // Semantic file-type colours (fixed, not theme-swapped), from the shared palette.
    private static readonly IBrush Blue = SemanticBrushes.Blue;
    private static readonly IBrush Green = SemanticBrushes.Green;
    private static readonly IBrush Purple = SemanticBrushes.Purple;
    private static readonly IBrush Yellow = SemanticBrushes.Yellow;
    private static readonly IBrush Red = SemanticBrushes.Red;
    private static readonly IBrush Neutral = SemanticBrushes.Neutral;

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

    /// <summary>Coarse category used by the file-list filter chips.</summary>
    public static FileCategory CategoryOf(string extension) => extension.ToLowerInvariant() switch {
        ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".svg" or ".webp" or ".ico" or ".tiff"
            => FileCategory.Image,
        ".zip" or ".rar" or ".7z" or ".tar" or ".gz" or ".bz2" or ".xz"
            => FileCategory.Archive,
        ".doc" or ".docx" or ".rtf" or ".odt" or ".pdf" or ".txt" or ".md" or ".csv"
            or ".xls" or ".xlsx" or ".ppt" or ".pptx" or ".json" or ".xml"
            => FileCategory.Document,
        _ => FileCategory.Other,
    };
}

/// <summary>Coarse file grouping behind the All / Documents / Images / Archives filter.</summary>
public enum FileCategory {
    Document,
    Image,
    Archive,
    Other,
}
