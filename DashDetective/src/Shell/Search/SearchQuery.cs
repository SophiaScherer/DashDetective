namespace DashDetective.Shell.Search;

/// <summary>
/// One request put to the search providers. Carries the caps as well as the term so a provider can stop
/// early — the file provider asks the Windows index for exactly <paramref name="PerCategoryLimit"/> rows
/// rather than fetching everything and throwing most of it away.
/// </summary>
/// <param name="Text">What the user typed, unnormalised. Matching is case-insensitive throughout.</param>
/// <param name="PerCategoryLimit">Most results any one provider may contribute.</param>
/// <param name="TotalLimit">Most results the merged list may hold.</param>
public readonly record struct SearchQuery(string Text, int PerCategoryLimit = 5, int TotalLimit = 20) {
    /// <summary>The term with surrounding whitespace removed — what providers should match against.</summary>
    public string Term => Text.Trim();

    /// <summary>Whether there is nothing to search for, so providers can be skipped entirely.</summary>
    public bool IsEmpty => Term.Length == 0;
}
