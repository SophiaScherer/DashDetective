namespace DashDetective.Shell.Search;

/// <summary>
/// What kind of thing a search result points at. Declared in the order the dropdown groups them, which
/// is also the tie-break order when two results score the same — so an exactly-named page wins over an
/// equally-named file.
/// </summary>
public enum SearchCategory {
    Page,
    Setting,
    Shortcut,
    Help,
    Toolkit,
    Process,
    File,
}
