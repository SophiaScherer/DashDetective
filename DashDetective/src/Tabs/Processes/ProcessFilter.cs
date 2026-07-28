using System;
using System.Globalization;

namespace DashDetective.Tabs.Processes;

/// <summary>The Processes tab's filter rule, kept as a pure static (like <c>ProcessTreeBuilder</c>) so
/// the matching behaviour is testable without a UI or a live process list.</summary>
public static class ProcessFilter {
    /// <summary>Whether a process matches the filter box: a case-insensitive substring of its name, or
    /// the start of its PID so a partly-typed number narrows as you go. A blank filter matches
    /// everything.</summary>
    public static bool Matches(string name, int pid, string? filter) {
        if (string.IsNullOrWhiteSpace(filter))
            return true;

        var term = filter.Trim();
        return name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
               pid.ToString(CultureInfo.InvariantCulture).StartsWith(term, StringComparison.Ordinal);
    }
}
