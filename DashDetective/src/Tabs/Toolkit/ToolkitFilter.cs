using System;
using System.Collections.Generic;

namespace DashDetective.Tabs.Toolkit;

/// <summary>The Toolkit tab's filter and grouping rules, kept as pure statics (like
/// <c>ProcessFilter</c>) so the behaviour is testable without a UI or any geometry.</summary>
public static class ToolkitFilter {
    /// <summary>Whether an entry survives the filter: it must be in the chosen category (a null
    /// category means "All") **and** match the search term. A blank term matches everything;
    /// otherwise it is a case-insensitive substring of the command or its description.
    ///
    /// A custom entry is in two categories at once — its own and the one the user labelled it with — so
    /// either satisfies the chip.</summary>
    public static bool Matches(ToolkitEntry entry, ToolkitCategory? category, string? term) {
        if (category is { } wanted && entry.Category != wanted && entry.SecondaryCategory != wanted)
            return false;

        if (string.IsNullOrWhiteSpace(term))
            return true;

        var text = term.Trim();
        return entry.Command.Contains(text, StringComparison.OrdinalIgnoreCase) ||
               entry.Description.Contains(text, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Filters the entries and buckets what is left into sections, in
    /// <see cref="ToolkitCatalog.Categories"/> order. A category with nothing left drops out entirely —
    /// header and card both — rather than showing an empty card.
    ///
    /// A pinned entry is **lifted** into the Pinned section rather than copied there: it is the one
    /// thing the user asked to be able to find in a fixed place, so leaving a second copy behind in its
    /// category would defeat the point. The chip and the search term still apply to it, so narrowing to
    /// one category does not drag unrelated pins along.
    ///
    /// A custom entry the user labelled with a category, by contrast, is deliberately shown **twice** —
    /// once under My Commands and once under that category — because they asked for it in both places.
    /// The view flashes every row carrying a revealed command precisely so that duplication stays safe.
    /// Picking a chip collapses it back to one: asking for Folders should not also produce a My Commands
    /// section.
    /// </summary>
    public static IReadOnlyList<ToolkitGroup> Group(
        IEnumerable<ToolkitEntry> entries, ToolkitCategory? category, string? term) {
        var pinned = new List<ToolkitEntry>();
        var buckets = new Dictionary<ToolkitCategory, List<ToolkitEntry>>();
        foreach (var entry in entries) {
            if (!Matches(entry, category, term))
                continue;

            if (entry.IsPinned) {
                pinned.Add(entry);
                continue;
            }

            foreach (var placement in PlacementsFor(entry)) {
                if (category is { } chosen && placement != chosen)
                    continue;

                if (!buckets.TryGetValue(placement, out var bucket))
                    buckets[placement] = bucket = [];

                bucket.Add(entry);
            }
        }

        var groups = new List<ToolkitGroup>();
        if (pinned.Count > 0)
            groups.Add(ToolkitGroup.Pinned(pinned));

        foreach (var known in ToolkitCatalog.Categories)
            if (buckets.TryGetValue(known, out var bucket))
                groups.Add(new ToolkitGroup(known, bucket));

        return groups;
    }

    /// <summary>Every section an entry belongs in: its own category, plus the one a custom entry was
    /// labelled with. Yielded in that order, so My Commands lists it before the labelled section
    /// does.</summary>
    private static IEnumerable<ToolkitCategory> PlacementsFor(ToolkitEntry entry) {
        yield return entry.Category;

        if (entry.SecondaryCategory is { } second && second != entry.Category)
            yield return second;
    }
}
