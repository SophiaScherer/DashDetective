using System;
using System.Collections.Generic;

namespace DashDetective.Services.Diagnostics;

/// <summary>The file types a system report can be saved as.</summary>
public enum DiagnosticsFormat {
    Text,
    Json,
    Markdown,
    Html,
    Csv,
}

/// <summary>One format's file identity: what it is called in the save dialog, and the extension it
/// carries.</summary>
public sealed record DiagnosticsFormatInfo(DiagnosticsFormat Format, string Extension, string TypeName);

/// <summary>
/// The single source of truth for the export formats — what each is called, what it saves as, and how a
/// report is rendered into it. Held as a static table like <c>ShortcutCatalog</c> and
/// <c>SettingCatalog</c>, so adding a sixth format is one entry plus one switch arm rather than an edit
/// in each of the three places that own a save dialog.
/// </summary>
public static class DiagnosticsFormats {
    /// <summary>Every format, in the order the save dialog offers them. Text leads because it is the
    /// default and what the app exported before.</summary>
    public static IReadOnlyList<DiagnosticsFormatInfo> All { get; } = [
        new(DiagnosticsFormat.Text, "txt", "Text report"),
        new(DiagnosticsFormat.Json, "json", "JSON"),
        new(DiagnosticsFormat.Markdown, "md", "Markdown"),
        new(DiagnosticsFormat.Html, "html", "HTML page"),
        new(DiagnosticsFormat.Csv, "csv", "CSV spreadsheet"),
    ];

    /// <summary>Every format a system report can be saved as, in dialog order — what the Export actions
    /// offer. Derived from <see cref="All"/> so the two can never list different things.</summary>
    public static IReadOnlyList<DiagnosticsFormat> Offered { get; } = BuildOffered();

    private static IReadOnlyList<DiagnosticsFormat> BuildOffered() {
        var formats = new DiagnosticsFormat[All.Count];
        for (var i = 0; i < All.Count; i++)
            formats[i] = All[i].Format;
        return formats;
    }

    /// <summary>The file identity of a format. Throws for one with no entry, which would mean the enum
    /// and this table have drifted apart — a bug, not a runtime condition (and one the tests catch).</summary>
    public static DiagnosticsFormatInfo Info(DiagnosticsFormat format) {
        foreach (var info in All)
            if (info.Format == format)
                return info;

        throw new ArgumentOutOfRangeException(nameof(format), format, "No entry for this export format.");
    }

    /// <summary>
    /// Which format a chosen filename means. The save dialog is asked for the format back this way rather
    /// than from the picked filter: Avalonia does not report which filter was selected, and the name is
    /// what the user actually sees and can override by typing an extension.
    /// </summary>
    public static DiagnosticsFormat FromFileName(string? fileName) {
        if (string.IsNullOrWhiteSpace(fileName))
            return DiagnosticsFormat.Text;

        var dot = fileName.LastIndexOf('.');
        if (dot < 0)
            return DiagnosticsFormat.Text;

        var extension = fileName[(dot + 1)..];
        foreach (var info in All)
            if (string.Equals(info.Extension, extension, StringComparison.OrdinalIgnoreCase))
                return info.Format;

        return DiagnosticsFormat.Text;   // an unrecognised extension still gets a readable file
    }

    /// <summary>Renders a report in the given format — the one place the formatters are chosen between.</summary>
    public static string Render(DiagnosticsReport report, DiagnosticsFormat format) => format switch {
        DiagnosticsFormat.Json => JsonReportFormatter.Format(report),
        DiagnosticsFormat.Markdown => MarkdownReportFormatter.Format(report),
        DiagnosticsFormat.Html => HtmlReportFormatter.Format(report),
        DiagnosticsFormat.Csv => CsvReportFormatter.Format(report),
        _ => TextReportFormatter.Format(report),
    };
}
