using Avalonia.Automation;
using Avalonia.Controls;
using DashDetective.Shared.Controls;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DashDetective.Shell.Navigation;

/// <summary>A titled widget found on the page, where it was laid out, and whether it is on screen. The
/// view reads these off the visual tree; everything after that is <see cref="PageSections"/>'s.</summary>
/// <param name="Title">The widget's heading, or null for a widget without one.</param>
/// <param name="Top">Its arranged top edge, relative to the page.</param>
/// <param name="Left">Its arranged left edge, relative to the page.</param>
/// <param name="IsVisible">Whether it is effectively visible.</param>
internal readonly record struct SectionCandidate(string? Title, double Top, double Left, bool IsVisible);

/// <summary>One entry of the nav bar's section list.</summary>
/// <param name="Title">The heading shown in the list.</param>
/// <param name="Source">Its position in the candidate list it came from, which is how the view finds the
/// widget again.</param>
public sealed record PageSection(string Title, int Source);

/// <summary>
/// Decides which widgets on a page are sections, and in what order. Pure, so it is testable without a
/// layout pass: the view hands in what it measured.
/// </summary>
internal static class PageSections {
    /// <summary>Fewer than this and there is nothing to jump between, so the list is not offered.</summary>
    internal const int MinimumToShow = 2;

    /// <summary>How far apart two tops can be and still read as one row. A board aligns a row's tops
    /// exactly; the slack is for panels whose margins differ by a pixel or two.</summary>
    internal const double RowTolerance = 4;

    /// <summary>The class a page puts on a card that is a section without being a <see cref="WidgetPanel"/>,
    /// such as a Hardware card; its title is its automation name.</summary>
    internal const string SectionClass = "pageSection";

    /// <summary>Whether a control is a section: a widget, or a card marked with <see cref="SectionClass"/>.
    /// Untitled ones still count here, so a titled widget nested in one is not listed on its own.</summary>
    internal static bool IsSection(Control control) =>
        control is WidgetPanel || control.Classes.Contains(SectionClass);

    /// <summary>The heading a section is listed under: a widget's title, or a marked card's automation name.</summary>
    internal static string? TitleOf(Control control) =>
        control is WidgetPanel widget ? widget.Title : AutomationProperties.GetName(control);

    /// <summary>Whether a page with this many sections offers the list.</summary>
    internal static bool ShouldShow(int count) => count >= MinimumToShow;

    /// <summary>The visible, titled candidates in reading order: rows top to bottom, then left to right
    /// within a row. Ordered by where they were arranged rather than declared, since widgets are
    /// reordered by dragging.</summary>
    internal static IReadOnlyList<PageSection> Discover(IReadOnlyList<SectionCandidate> candidates) {
        var shown = candidates
            .Select((candidate, index) => (Candidate: candidate, Index: index))
            .Where(c => c.Candidate.IsVisible && !string.IsNullOrWhiteSpace(c.Candidate.Title)
                        && double.IsFinite(c.Candidate.Top) && double.IsFinite(c.Candidate.Left))
            .OrderBy(c => c.Candidate.Top)
            .ToList();

        // Group into rows against each row's first top, so a long run of near-equal tops cannot drift.
        var sections = new List<PageSection>(shown.Count);
        var row = new List<(SectionCandidate Candidate, int Index)>();
        var rowTop = double.NaN;

        foreach (var c in shown) {
            if (row.Count > 0 && Math.Abs(c.Candidate.Top - rowTop) > RowTolerance)
                Flush();
            if (row.Count == 0)
                rowTop = c.Candidate.Top;
            row.Add(c);
        }
        Flush();

        return sections;

        void Flush() {
            foreach (var c in row.OrderBy(c => c.Candidate.Left))
                sections.Add(new PageSection(c.Candidate.Title!.Trim(), c.Index));
            row.Clear();
        }
    }
}
