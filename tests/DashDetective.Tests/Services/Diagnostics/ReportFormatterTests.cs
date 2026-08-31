using DashDetective.Services.Diagnostics;
using System;
using System.Text.Json;
using Xunit;

namespace DashDetective.Tests.Services.Diagnostics;

/// <summary>
/// Covers the export formats: that the text one still renders exactly what the app shipped, that every
/// other one escapes the characters its syntax reserves, and that a filename maps back to the format it
/// names. The nasty values are not hypothetical — a DNS list is comma-separated, the throughput row
/// carries arrows and slashes, and a machine or Toolkit command name is user-controlled text.
/// </summary>
public class ReportFormatterTests {
    private static readonly DateTime Moment = new(2026, 8, 29, 14, 5, 9);

    private static DiagnosticsReport Report(params ReportSection[] sections) =>
        new("DashDetective — System Report", Moment, sections);

    private static DiagnosticsReport Sample() => Report(
        new ReportSection("System", [
            new ReportRow("OS", "Windows 11 Pro 25H2"),
            new ReportRow("Device", "DESKTOP-OFKHPOQ"),
        ]),
        new ReportSection("Live metrics", [
            new ReportRow("CPU", "11%  (AMD Ryzen 5 7600X)"),
        ]));

    /// <summary>A value carrying every character the formats reserve between them.</summary>
    private static DiagnosticsReport Hostile() => Report(
        new ReportSection("Network configuration", [
            new ReportRow("DNS", "1.1.1.1, 8.8.8.8"),
            new ReportRow("Adapter", "Wi-Fi \"2\" | <script>alert(1)</script>"),
        ]));

    // ----- Text: the format that must not change -----

    /// <summary>Pins the layout byte for byte. An existing saved report and a new one still have to diff
    /// cleanly, so the two-space indent and the 14-column key padding are part of the contract.</summary>
    [Fact]
    public void Text_RendersTheLayoutTheAppShipped() {
        var nl = Environment.NewLine;
        var expected =
            $"DashDetective — System Report{nl}" +
            $"Generated: 2026-08-29 14:05:09{nl}" +
            $"{nl}" +
            $"System{nl}" +
            $"  OS:           Windows 11 Pro 25H2{nl}" +
            $"  Device:       DESKTOP-OFKHPOQ{nl}" +
            $"{nl}" +
            $"Live metrics{nl}" +
            $"  CPU:          11%  (AMD Ryzen 5 7600X){nl}";

        Assert.Equal(expected, TextReportFormatter.Format(Sample()));
    }

    /// <summary>A key longer than the padding column must not be truncated — it pushes its value right.</summary>
    [Fact]
    public void Text_LongKeyIsNotTruncated() {
        var report = Report(new ReportSection("System",
            [new ReportRow("A very long field name", "value")]));

        Assert.Contains("  A very long field name:value", TextReportFormatter.Format(report),
                        StringComparison.Ordinal);
    }

    // ----- Markdown -----

    [Fact]
    public void Markdown_RendersASectionHeadingAndATable() {
        var markdown = MarkdownReportFormatter.Format(Sample());

        Assert.Contains("# DashDetective — System Report", markdown, StringComparison.Ordinal);
        Assert.Contains("## System", markdown, StringComparison.Ordinal);
        Assert.Contains("| Field | Value |", markdown, StringComparison.Ordinal);
        Assert.Contains("| OS | Windows 11 Pro 25H2 |", markdown, StringComparison.Ordinal);
    }

    /// <summary>An unescaped pipe ends the cell early, silently splitting one value into two columns.</summary>
    [Fact]
    public void Markdown_EscapesAPipeInAValue() {
        var markdown = MarkdownReportFormatter.Format(Hostile());

        Assert.Contains("\\|", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("\"2\" | <script>", markdown, StringComparison.Ordinal);
    }

    // ----- HTML -----

    [Fact]
    public void Html_IsSelfContained() {
        var html = HtmlReportFormatter.Format(Sample());

        Assert.StartsWith("<!DOCTYPE html>", html, StringComparison.Ordinal);
        Assert.Contains("<style>", html, StringComparison.Ordinal);
        Assert.EndsWith("</html>" + Environment.NewLine, html, StringComparison.Ordinal);

        // Nothing may reach outside the file — that is the whole point of "self-contained".
        Assert.DoesNotContain("<link", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The values are user-controlled text, so an unescaped "&lt;" would swallow the rest of the
    /// row at best and inject markup at worst.</summary>
    [Fact]
    public void Html_EscapesMarkupInAValue() {
        var html = HtmlReportFormatter.Format(Hostile());

        Assert.Contains("&lt;script&gt;", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>alert", html, StringComparison.Ordinal);
    }

    // ----- CSV -----

    [Fact]
    public void Csv_LeadsWithAHeaderAndCarriesTheSection() {
        var csv = CsvReportFormatter.Format(Sample());

        Assert.StartsWith("section,field,value\n", csv, StringComparison.Ordinal);
        Assert.Contains("System,OS,Windows 11 Pro 25H2\n", csv, StringComparison.Ordinal);
    }

    /// <summary>A DNS list is comma-separated, so an unquoted value would shift every later column.</summary>
    [Fact]
    public void Csv_QuotesAValueHoldingACommaAndDoublesItsQuotes() {
        var csv = CsvReportFormatter.Format(Hostile());

        Assert.Contains("\"1.1.1.1, 8.8.8.8\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"\"2\"\"", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void Csv_LeavesAPlainValueUnquoted() =>
        Assert.Contains("System,Device,DESKTOP-OFKHPOQ", CsvReportFormatter.Format(Sample()),
                        StringComparison.Ordinal);

    // ----- JSON -----

    [Fact]
    public void Json_RoundTripsTheReport() {
        var json = JsonReportFormatter.Format(Sample());

        using var parsed = JsonDocument.Parse(json);
        var root = parsed.RootElement;

        Assert.Equal("DashDetective — System Report", root.GetProperty("Title").GetString());
        var sections = root.GetProperty("Sections");
        Assert.Equal(2, sections.GetArrayLength());
        Assert.Equal("System", sections[0].GetProperty("Title").GetString());
        Assert.Equal("OS", sections[0].GetProperty("Rows")[0].GetProperty("Key").GetString());
    }

    /// <summary>The serializer owns the escaping, so this only has to prove nothing is lost.</summary>
    [Fact]
    public void Json_KeepsAHostileValueIntact() {
        using var parsed = JsonDocument.Parse(JsonReportFormatter.Format(Hostile()));

        var adapter = parsed.RootElement.GetProperty("Sections")[0].GetProperty("Rows")[1];
        Assert.Equal("Wi-Fi \"2\" | <script>alert(1)</script>", adapter.GetProperty("Value").GetString());
    }

    // ----- The format catalog -----

    /// <summary>Every enum member needs an entry, or the save dialog throws when it is offered.</summary>
    [Fact]
    public void EveryFormatHasAnEntry() {
        foreach (var format in Enum.GetValues<DiagnosticsFormat>())
            Assert.Equal(format, DiagnosticsFormats.Info(format).Format);

        Assert.Equal(Enum.GetValues<DiagnosticsFormat>().Length, DiagnosticsFormats.All.Count);
        Assert.Equal(DiagnosticsFormats.All.Count, DiagnosticsFormats.Offered.Count);
    }

    [Theory]
    [InlineData("report.txt", DiagnosticsFormat.Text)]
    [InlineData("report.json", DiagnosticsFormat.Json)]
    [InlineData("report.md", DiagnosticsFormat.Markdown)]
    [InlineData("report.html", DiagnosticsFormat.Html)]
    [InlineData("report.csv", DiagnosticsFormat.Csv)]
    [InlineData("REPORT.HTML", DiagnosticsFormat.Html)]
    [InlineData("my.report.2026.md", DiagnosticsFormat.Markdown)]
    public void FromFileName_ReadsTheExtension(string name, DiagnosticsFormat expected) =>
        Assert.Equal(expected, DiagnosticsFormats.FromFileName(name));

    /// <summary>A name with no extension, or one nobody recognises, still has to produce a readable file
    /// rather than throwing in the middle of a save.</summary>
    [Theory]
    [InlineData("report")]
    [InlineData("report.")]
    [InlineData("report.xyz")]
    [InlineData("")]
    [InlineData(null)]
    public void FromFileName_FallsBackToText(string? name) =>
        Assert.Equal(DiagnosticsFormat.Text, DiagnosticsFormats.FromFileName(name));

    /// <summary>Render has to reach the right formatter for every member, not just the ones with a case.</summary>
    [Fact]
    public void Render_MatchesTheFormatterForEveryFormat() {
        var report = Sample();

        Assert.Equal(TextReportFormatter.Format(report), DiagnosticsFormats.Render(report, DiagnosticsFormat.Text));
        Assert.Equal(JsonReportFormatter.Format(report), DiagnosticsFormats.Render(report, DiagnosticsFormat.Json));
        Assert.Equal(MarkdownReportFormatter.Format(report), DiagnosticsFormats.Render(report, DiagnosticsFormat.Markdown));
        Assert.Equal(HtmlReportFormatter.Format(report), DiagnosticsFormats.Render(report, DiagnosticsFormat.Html));
        Assert.Equal(CsvReportFormatter.Format(report), DiagnosticsFormats.Render(report, DiagnosticsFormat.Csv));
    }

    /// <summary>A report with no sections is what a locked-down host can produce, and every format has to
    /// still write a valid file rather than something half-formed.</summary>
    [Fact]
    public void EmptyReport_StillRendersInEveryFormat() {
        var empty = Report();

        foreach (var format in Enum.GetValues<DiagnosticsFormat>())
            Assert.False(string.IsNullOrWhiteSpace(DiagnosticsFormats.Render(empty, format)));
    }
}
