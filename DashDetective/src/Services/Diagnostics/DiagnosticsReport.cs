using System;
using System.Collections.Generic;

namespace DashDetective.Services.Diagnostics;

/// <summary>One "key: value" line of a report section.</summary>
public sealed record ReportRow(string Key, string Value);

/// <summary>A titled block of rows — "System", "Hardware", "Storage".</summary>
public sealed record ReportSection(string Title, IReadOnlyList<ReportRow> Rows);

/// <summary>
/// The system report as data rather than as text. The report used to be built by string concatenation
/// spread across the shell and the Dashboard, which meant one format and no way to add another; giving it
/// a shape lets every formatter render the same content, and lets the pages keep supplying plain
/// key/value rows.
/// </summary>
public sealed record DiagnosticsReport(string Title, DateTime GeneratedAt, IReadOnlyList<ReportSection> Sections);
