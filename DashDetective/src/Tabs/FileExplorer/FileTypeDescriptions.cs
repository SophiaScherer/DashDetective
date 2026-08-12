using System;
using System.IO;

namespace DashDetective.Tabs.FileExplorer;

/// <summary>
/// Friendly type descriptions from a file's name — the table that stands in for the shell lookup Linux
/// has no cheap equivalent of. <c>xdg-mime query filetype</c> would be authoritative, but it is a
/// subprocess per row and this is called once for every entry in a folder.
///
/// <b>Deliberately not on <see cref="FileTypeCatalog"/></b>, which is the obvious home: that class's
/// static initialiser calls <c>Geometry.Parse</c> and needs a render backend the test project does not
/// have, so a map living there could not be unit-tested at all. Nothing here touches Avalonia.
///
/// The wording follows the desktop's own convention ("PNG image", "JSON document"), which is why this
/// does not reuse <c>ShellFallback.TypeName</c> — that produces Windows' "JSON File" casing.
/// </summary>
internal static class FileTypeDescriptions {
    /// <summary>The description for a file, always something. A name with no usable extension is
    /// reported by what it is: a leading dot means hidden on this platform.</summary>
    internal static string For(string path) {
        var name = Path.GetFileName(path);
        var dot = name.LastIndexOf('.');

        // A dot at index 0 is the hidden-file marker, not an extension — ".bashrc" is not a BASHRC file.
        if (dot <= 0 || dot == name.Length - 1)
            return name.StartsWith('.') ? "Hidden file" : "File";

        var extension = name[dot..].ToLowerInvariant();
        return Known(extension) ?? $"{extension[1..].ToUpperInvariant()} file";
    }

    private static string? Known(string extension) => extension switch {
        // Documents
        ".pdf" => "PDF document",
        ".txt" => "Plain text document",
        ".md" => "Markdown document",
        ".rtf" => "Rich text document",
        ".doc" or ".docx" => "Word document",
        ".odt" => "OpenDocument text",
        ".xls" or ".xlsx" => "Excel spreadsheet",
        ".ods" => "OpenDocument spreadsheet",
        ".csv" => "CSV document",
        ".ppt" or ".pptx" => "PowerPoint presentation",
        ".odp" => "OpenDocument presentation",
        ".epub" => "EPUB document",

        // Images
        ".png" => "PNG image",
        ".jpg" or ".jpeg" => "JPEG image",
        ".gif" => "GIF image",
        ".bmp" => "BMP image",
        ".svg" => "SVG image",
        ".webp" => "WebP image",
        ".tif" or ".tiff" => "TIFF image",
        ".ico" => "Icon",

        // Archives and packages
        ".zip" => "ZIP archive",
        ".tar" => "Tar archive",
        ".gz" or ".tgz" => "Gzip archive",
        ".bz2" => "Bzip2 archive",
        ".xz" => "XZ archive",
        ".zst" => "Zstandard archive",
        ".7z" => "7-Zip archive",
        ".rar" => "RAR archive",
        ".deb" => "Debian package",
        ".rpm" => "RPM package",
        ".flatpakref" => "Flatpak reference",
        ".appimage" => "AppImage",
        ".iso" => "Disc image",

        // Audio and video
        ".mp3" => "MP3 audio",
        ".flac" => "FLAC audio",
        ".ogg" or ".oga" => "Ogg audio",
        ".wav" => "WAV audio",
        ".m4a" => "MPEG-4 audio",
        ".opus" => "Opus audio",
        ".mp4" or ".m4v" => "MPEG-4 video",
        ".mkv" => "Matroska video",
        ".webm" => "WebM video",
        ".avi" => "AVI video",
        ".mov" => "QuickTime video",

        // Markup, data and code
        ".json" => "JSON document",
        ".xml" => "XML document",
        ".yml" or ".yaml" => "YAML document",
        ".toml" => "TOML document",
        ".ini" or ".conf" or ".cfg" => "Configuration file",
        ".html" or ".htm" => "HTML document",
        ".css" => "CSS stylesheet",
        ".js" => "JavaScript source",
        ".ts" => "TypeScript source",
        ".py" => "Python script",
        ".sh" or ".bash" => "Shell script",
        ".pl" => "Perl script",
        ".rb" => "Ruby script",
        ".c" => "C source",
        ".h" => "C header",
        ".cpp" or ".cc" or ".cxx" => "C++ source",
        ".cs" => "C# source",
        ".rs" => "Rust source",
        ".go" => "Go source",
        ".java" => "Java source",
        ".sql" => "SQL script",
        ".patch" or ".diff" => "Patch file",

        // Linux system files
        ".desktop" => "Desktop entry",
        ".service" or ".socket" or ".timer" => "Systemd unit",
        ".so" => "Shared library",
        ".ko" => "Kernel module",
        ".log" => "Log file",
        ".pem" or ".crt" or ".cer" => "Certificate",
        ".ttf" => "TrueType font",
        ".otf" => "OpenType font",

        _ => null,
    };
}
