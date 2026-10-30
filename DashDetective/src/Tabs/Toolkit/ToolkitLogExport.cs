using DashDetective.Services.Diagnostics;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace DashDetective.Tabs.Toolkit;

/// <summary>
/// The Execution Log as a <see cref="DiagnosticsReport"/>, so the log exports in every format the system
/// report does through the same renderers. Text is the exception: it keeps the transcript layout users
/// already rely on, so it is not rendered from this model.
/// </summary>
internal static class ToolkitLogExport {
    /// <summary>One section per stanza, in the order given (the panel's, newest first). Timestamps are
    /// 24-hour whatever the clock preference, as in the text export, so a file stays sortable.</summary>
    public static DiagnosticsReport BuildReport(IEnumerable<ToolkitLogEntry> entries, DateTime generatedAt) {
        var sections = new List<ReportSection>();
        var number = 1;
        foreach (var entry in entries) {
            sections.Add(new ReportSection($"Run {number++}", [
                new ReportRow("Started", entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),
                new ReportRow("Command", entry.Command),
                // The newline a command ends its output with is layout, not content, and would leave an
                // empty last line in a Markdown cell.
                new ReportRow("Output", entry.Output.TrimEnd('\r', '\n')),
            ]));
        }

        return new DiagnosticsReport("Execution log", generatedAt, sections);
    }
}
