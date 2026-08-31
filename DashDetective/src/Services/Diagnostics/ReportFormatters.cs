using System;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;

namespace DashDetective.Services.Diagnostics;

/// <summary>
/// Renders a <see cref="DiagnosticsReport"/> as plain text — the layout the app has always exported, kept
/// byte for byte so an existing saved report and a new one still diff cleanly: a two-space indent and the
/// key padded to 14 columns.
/// </summary>
public static class TextReportFormatter {
    private const int KeyWidth = 14;

    public static string Format(DiagnosticsReport report) {
        var sb = new StringBuilder();
        sb.AppendLine(report.Title);
        sb.AppendLine($"Generated: {ReportTimestamp.Format(report.GeneratedAt)}");

        foreach (var section in report.Sections) {
            sb.AppendLine();
            sb.AppendLine(section.Title);
            foreach (var row in section.Rows)
                sb.AppendLine($"  {(row.Key + ":").PadRight(KeyWidth)}{row.Value}");
        }

        return sb.ToString();
    }
}

/// <summary>Renders a report as Markdown: a heading per section and a two-column table of its rows.</summary>
public static class MarkdownReportFormatter {
    public static string Format(DiagnosticsReport report) {
        var sb = new StringBuilder();
        sb.AppendLine($"# {report.Title}");
        sb.AppendLine();
        sb.AppendLine($"Generated: {ReportTimestamp.Format(report.GeneratedAt)}");

        foreach (var section in report.Sections) {
            sb.AppendLine();
            sb.AppendLine($"## {section.Title}");
            sb.AppendLine();
            sb.AppendLine("| Field | Value |");
            sb.AppendLine("| --- | --- |");
            foreach (var row in section.Rows)
                sb.AppendLine($"| {Escape(row.Key)} | {Escape(row.Value)} |");
        }

        return sb.ToString();
    }

    /// <summary>A pipe would end the table cell early, and the values carry them (the Network adapter
    /// names and the "↓ x / ↑ y" throughput line are the realistic sources).</summary>
    private static string Escape(string value) => value.Replace("|", "\\|", StringComparison.Ordinal);
}

/// <summary>
/// Renders a report as a self-contained HTML page — inline styles, no external assets, so it opens
/// anywhere and can be attached to a ticket as one file.
/// </summary>
public static class HtmlReportFormatter {
    public static string Format(DiagnosticsReport report) {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset=\"utf-8\">");
        sb.AppendLine($"<title>{Escape(report.Title)}</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("body{font-family:system-ui,-apple-system,Segoe UI,sans-serif;margin:2rem auto;max-width:52rem;padding:0 1rem;color:#1a1a1a}");
        sb.AppendLine("h1{font-size:1.5rem;margin-bottom:.25rem}h2{font-size:1.05rem;margin-top:2rem}");
        sb.AppendLine(".generated{color:#666;font-size:.875rem;margin-top:0}");
        sb.AppendLine("table{border-collapse:collapse;width:100%}");
        sb.AppendLine("td{border-top:1px solid #e5e5e5;padding:.4rem .5rem;vertical-align:top}");
        sb.AppendLine("td.k{color:#666;width:14rem}");
        sb.AppendLine("@media(prefers-color-scheme:dark){body{background:#161616;color:#ededed}");
        sb.AppendLine(".generated,td.k{color:#9a9a9a}td{border-top-color:#2c2c2c}}");
        sb.AppendLine("</style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine($"<h1>{Escape(report.Title)}</h1>");
        sb.AppendLine($"<p class=\"generated\">Generated: {Escape(ReportTimestamp.Format(report.GeneratedAt))}</p>");

        foreach (var section in report.Sections) {
            sb.AppendLine($"<h2>{Escape(section.Title)}</h2>");
            sb.AppendLine("<table>");
            foreach (var row in section.Rows)
                sb.AppendLine($"<tr><td class=\"k\">{Escape(row.Key)}</td><td>{Escape(row.Value)}</td></tr>");
            sb.AppendLine("</table>");
        }

        sb.AppendLine("</body>");
        sb.AppendLine("</html>");
        return sb.ToString();
    }

    /// <summary>Every value goes through this. A machine name or a Toolkit command is user-controlled
    /// text, and an unescaped "&lt;" would silently swallow the rest of the row.</summary>
    private static string Escape(string value) => WebUtility.HtmlEncode(value);
}

/// <summary>
/// Renders a report as CSV, one row per field with its section beside it, so the whole report opens in a
/// spreadsheet. Distinct from the metrics CSV, which is the rolling sample history rather than this.
/// </summary>
public static class CsvReportFormatter {
    public static string Format(DiagnosticsReport report) {
        var sb = new StringBuilder();
        sb.Append("section,field,value").Append('\n');

        foreach (var section in report.Sections)
            foreach (var row in section.Rows)
                sb.Append(Escape(section.Title)).Append(',')
                  .Append(Escape(row.Key)).Append(',')
                  .Append(Escape(row.Value))
                  .Append('\n');

        return sb.ToString();
    }

    /// <summary>RFC 4180 quoting: wrap anything holding a comma, a quote or a newline, and double the
    /// quotes inside. The values carry commas routinely — every DNS list is one.</summary>
    private static string Escape(string value) {
        if (value.AsSpan().IndexOfAny(",\"\r\n") < 0)
            return value;

        return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }
}

/// <summary>Renders a report as JSON, through the source-generated context the trimming/AOT gate
/// requires.</summary>
public static class JsonReportFormatter {
    public static string Format(DiagnosticsReport report) =>
        JsonSerializer.Serialize(report, DiagnosticsJsonContext.Default.DiagnosticsReport);
}

/// <summary>The one timestamp format every report carries. Deliberately 24-hour and sortable whatever the
/// clock-format preference is set to — a report is a file, and files get sorted and parsed.</summary>
internal static class ReportTimestamp {
    public static string Format(DateTime moment) =>
        moment.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
}
