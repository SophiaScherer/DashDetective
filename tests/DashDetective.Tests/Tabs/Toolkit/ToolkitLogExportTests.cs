using DashDetective.Services.Diagnostics;
using DashDetective.Shared;
using DashDetective.Tabs.Toolkit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using Xunit;

namespace DashDetective.Tests.Tabs.Toolkit;

/// <summary>
/// Pins the Execution Log's report model and that each shared renderer copes with real command output:
/// several lines, commas, quotes, pipes and angle brackets. The model has one section per stanza in the
/// panel's order, and every format must round-trip the hostile output rather than lose or split it.
/// </summary>
public class ToolkitLogExportTests {
    private static readonly DateTime Moment = new(2026, 10, 13, 9, 30, 15);

    // A directory listing is the realistic offender: <DIR>, commas in sizes, a pipe, a quoted name.
    private const string Hostile = "line one, with comma\r\n \"quoted\" | pipe <DIR> & amp\r\nlast line\r\n";
    private const string HostileTrimmed = "line one, with comma\r\n \"quoted\" | pipe <DIR> & amp\r\nlast line";

    private static ToolkitLogEntry Entry(string command, string output, DateTime? at = null) =>
        new(at ?? Moment, command, output, ClockFormat.TwentyFourHour);

    private static List<ToolkitLogEntry> Log() => [
        Entry("dir \"C:\\a,b\"", Hostile, Moment.AddMinutes(1)),
        Entry("ipconfig /all", "Windows IP Configuration", Moment),
    ];

    private static DiagnosticsReport Report() => ToolkitLogExport.BuildReport(Log(), Moment.AddHours(1));

    /// <summary>A minimal RFC 4180 reader, so the CSV assertion checks fields rather than substrings.</summary>
    private static List<List<string>> ParseCsv(string csv) {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < csv.Length; i++) {
            var c = csv[i];
            if (quoted) {
                if (c == '"' && i + 1 < csv.Length && csv[i + 1] == '"') {
                    field.Append('"');
                    i++;
                } else if (c == '"') {
                    quoted = false;
                } else {
                    field.Append(c);
                }
            } else if (c == '"') {
                quoted = true;
            } else if (c == ',') {
                row.Add(field.ToString());
                field.Clear();
            } else if (c == '\n') {
                row.Add(field.ToString());
                field.Clear();
                rows.Add(row);
                row = [];
            } else {
                field.Append(c);
            }
        }

        return rows;
    }

    [Fact]
    public void BuildReport_OneSectionPerStanza_InTheOrderGiven() {
        var report = Report();

        Assert.Equal(2, report.Sections.Count);
        Assert.Contains("dir", report.Sections[0].Rows.Single(r => r.Key == "Command").Value, StringComparison.Ordinal);
        Assert.Contains("ipconfig", report.Sections[1].Rows.Single(r => r.Key == "Command").Value, StringComparison.Ordinal);
        Assert.NotEqual(report.Sections[0].Title, report.Sections[1].Title);
    }

    [Fact]
    public void BuildReport_Rows_CarryStartedCommandAndOutput() {
        var rows = Report().Sections[0].Rows;

        Assert.Equal(["Started", "Command", "Output"], rows.Select(r => r.Key));
        Assert.Equal("2026-10-13 09:31:15", rows[0].Value);
        Assert.Equal("dir \"C:\\a,b\"", rows[1].Value);
        Assert.Equal(HostileTrimmed, rows[2].Value);
    }

    [Fact]
    public void BuildReport_EmptyLog_HasNoSections() {
        var report = ToolkitLogExport.BuildReport([], Moment);

        Assert.Empty(report.Sections);
        Assert.Equal("Execution log", report.Title);
    }

    [Fact]
    public void Json_RoundTripsTheOutputExactly() {
        var json = DiagnosticsFormats.Render(Report(), DiagnosticsFormat.Json);

        using var doc = JsonDocument.Parse(json);
        var sections = doc.RootElement.GetProperty("Sections");
        Assert.Equal(2, sections.GetArrayLength());
        var output = sections[0].GetProperty("Rows")[2].GetProperty("Value").GetString();
        Assert.Equal(HostileTrimmed, output);
    }

    [Fact]
    public void Csv_KeepsEveryFieldWhole_WhenOutputHoldsCommasQuotesAndNewlines() {
        var csv = DiagnosticsFormats.Render(Report(), DiagnosticsFormat.Csv);

        var rows = ParseCsv(csv);

        Assert.Equal(1 + 2 * 3, rows.Count);
        Assert.All(rows, r => Assert.Equal(3, r.Count));
        Assert.Equal(HostileTrimmed, rows[3][2]);
        Assert.Equal("dir \"C:\\a,b\"", rows[2][2]);
    }

    [Fact]
    public void Html_EscapesTheOutput() {
        var html = DiagnosticsFormats.Render(Report(), DiagnosticsFormat.Html);

        Assert.Contains("&lt;DIR&gt;", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<DIR>", html, StringComparison.Ordinal);
        Assert.Contains("&quot;quoted&quot;", html, StringComparison.Ordinal);
    }

    /// <summary>A raw newline ends a table row, so multi-line output must stay inside its one cell.</summary>
    [Fact]
    public void Markdown_KeepsMultiLineOutputInOneTableRow() {
        var markdown = DiagnosticsFormats.Render(Report(), DiagnosticsFormat.Markdown);

        var lines = markdown.Split(["\r\n", "\n"], StringSplitOptions.None);
        var outputRow = Assert.Single(lines, l => l.StartsWith("| Output | line one", StringComparison.Ordinal));
        Assert.Contains("<br>", outputRow, StringComparison.Ordinal);
        Assert.Contains("last line", outputRow, StringComparison.Ordinal);
        Assert.Contains("&lt;DIR>", outputRow, StringComparison.Ordinal);
        Assert.Contains("\\|", outputRow, StringComparison.Ordinal);

        // Every line of a section's table is a table row; none of the output spilled out of it.
        var tableLines = lines.SkipWhile(l => !l.StartsWith("| Field", StringComparison.Ordinal))
                              .TakeWhile(l => l.Length > 0);
        Assert.All(tableLines, l => Assert.StartsWith("|", l, StringComparison.Ordinal));
    }
}
