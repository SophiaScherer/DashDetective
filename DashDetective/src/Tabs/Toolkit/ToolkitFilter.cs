using System;
using System.Collections.Generic;

namespace DashDetective.Tabs.Toolkit;

/// <summary>The Toolkit tab's filter and grouping rules, kept as pure statics (like
/// <c>ProcessFilter</c>) so the behaviour is testable without a UI or any geometry.</summary>
public static class ToolkitFilter {
    /// <summary>Whether an entry survives the filter: it must be in the chosen category (a null
    /// category means "All") **and** match the search term. A blank term matches everything;
    /// otherwise it is a case-insensitive substring of the command or its description.</summary>
    public static bool Matches(ToolkitEntry entry, ToolkitCategory? category, string? term) {
        if (category is { } wanted && entry.Category != wanted)
            return false;

        if (string.IsNullOrWhiteSpace(term))
            return true;

        var text = term.Trim();
        return entry.Command.Contains(text, StringComparison.OrdinalIgnoreCase) ||
               entry.Description.Contains(text, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Filters the entries and buckets what is left into sections, in
    /// <see cref="ToolkitCatalog.Categories"/> order. A category with nothing left drops out
    /// entirely — header and card both — rather than showing an empty card.</summary>
    public static IReadOnlyList<ToolkitGroup> Group(
        IEnumerable<ToolkitEntry> entries, ToolkitCategory? category, string? term) {
        var buckets = new Dictionary<ToolkitCategory, List<ToolkitEntry>>();
        foreach (var entry in entries) {
            if (!Matches(entry, category, term))
                continue;

            if (!buckets.TryGetValue(entry.Category, out var bucket))
                buckets[entry.Category] = bucket = [];

            bucket.Add(entry);
        }

        var groups = new List<ToolkitGroup>();
        foreach (var known in ToolkitCatalog.Categories)
            if (buckets.TryGetValue(known, out var bucket))
                groups.Add(new ToolkitGroup(known, bucket));

        return groups;
    }
}
