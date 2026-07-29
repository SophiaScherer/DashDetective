using Avalonia.Media;
using System;

namespace DashDetective.Shell.Search;

/// <summary>
/// One row in the search dropdown, together with what picking it does. The provider that produced the
/// result owns the <paramref name="Activate"/> callback, so the dropdown never needs to know how to
/// reach a process, a setting or a folder — it just runs what it was handed.
/// </summary>
/// <param name="Category">Which group the row sits under.</param>
/// <param name="Title">The headline, e.g. a page name, a file name or a process name.</param>
/// <param name="Subtitle">One line of context: the folder a file sits in, a setting's section, a PID.</param>
/// <param name="Score">Match strength from <see cref="SearchRanker"/>; higher sorts first.</param>
/// <param name="Activate">Navigates to the thing and reveals it. Runs on the UI thread.</param>
/// <param name="Icon">The glyph shown beside the row, or null for the category's default.</param>
/// <param name="Completion">What Tab should fill the search box with, when this is the top result.
/// Null for results that aren't worth completing to (a page name the user already finished typing).</param>
public sealed record SearchResult(
    SearchCategory Category,
    string Title,
    string Subtitle,
    int Score,
    Action Activate,
    Geometry? Icon = null,
    string? Completion = null) {

    /// <summary>How the category reads on the row's tag. Kept here rather than in a converter so the
    /// dropdown's template stays a plain binding.</summary>
    public string CategoryLabel => Category switch {
        SearchCategory.Page => "Page",
        SearchCategory.Setting => "Setting",
        SearchCategory.Shortcut => "Shortcut",
        SearchCategory.Process => "Process",
        _ => "File",
    };
}
