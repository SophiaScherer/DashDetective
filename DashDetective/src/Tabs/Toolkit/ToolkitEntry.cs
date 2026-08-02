using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DashDetective.Tabs.Toolkit;

/// <summary>
/// One command row: what it is (command text, description, category, kind), what running it does
/// (<see cref="Action"/>), plus the presentation its kind implies. Immutable — nothing about a command
/// changes at runtime, and the search-reveal flash is owned by the view (see
/// <c>ToolkitView.OnRevealRequested</c>), as it is on the Settings page.
///
/// The visual getters resolve through <see cref="ToolkitIcons"/> **on read**, so an entry can be
/// built and filtered in a headless test without loading any geometry.
/// </summary>
public sealed partial class ToolkitEntry : ObservableObject {
    public ToolkitEntry(
        string command, string description, ToolkitCategory category, ToolkitEntryKind kind,
        ToolkitAction action, ToolkitParameter? parameter = null, ToolkitCommand? source = null) {
        Command = command;
        Description = description;
        Category = category;
        Kind = kind;
        Action = action;
        Parameter = parameter;
        Source = source;
    }

    /// <summary>The command itself — the row's primary label, and its identity for search reveal.</summary>
    public string Command { get; }

    /// <summary>One line on what the command does.</summary>
    public string Description { get; }

    public ToolkitCategory Category { get; }
    public ToolkitEntryKind Kind { get; }

    /// <summary>What running this row does. Authored in <see cref="ToolkitCatalog"/> and carried out by
    /// <see cref="ToolkitRunner"/> — the row itself never touches a process.</summary>
    public ToolkitAction Action { get; }

    /// <summary>The row's editable slot, or null for the rows that take no input (nearly all of them).
    /// The text box binds to it, so it is mutable where the rest of the entry is not.</summary>
    public ToolkitParameter? Parameter { get; }

    /// <summary>What the user typed to author this row, or null for a catalog row. Carried rather than
    /// reconstructed so the edit form can be re-filled with their own words — see
    /// <see cref="ToolkitCommandFactory"/>.</summary>
    public ToolkitCommand? Source { get; }

    /// <summary>Whether the user authored this row. Drives its edit and delete affordances, and the
    /// filter's placement rule.</summary>
    public bool IsCustom => Source is not null;

    /// <summary>The category a custom row asked to *also* appear under, or null. A catalog row has none
    /// — its one <see cref="Category"/> is the whole story.</summary>
    public ToolkitCategory? SecondaryCategory => Source?.Category;

    /// <summary>Whether the user has pinned this command to the top of the list. Observable so the star
    /// redraws on the row that was clicked, without the whole list having to be rebuilt to show it.
    /// Persisted by command text — see <see cref="ToolkitPins"/>.</summary>
    [ObservableProperty] private bool _isPinned;

    /// <summary>Whether running this raises a UAC prompt, so the row can warn before it is clicked
    /// rather than surprising the user with a consent dialog.</summary>
    public bool RequiresElevation => Action.RequiresElevation;

    /// <summary>Whether this row opens a location rather than running something — the rows that get the
    /// "open where?" pair of icons.</summary>
    public bool IsPathEntry => Action.Kind == ToolkitActionKind.OpenPath;

    /// <summary>Whether the app's own File Explorer could open this row. False for a <c>shell:</c>
    /// location, which resolves through the shell namespace and has no path to navigate to — those rows
    /// offer the external icon only.</summary>
    public bool CanOpenInApp => IsPathEntry && ToolkitPaths.IsFileSystemPath(Action.Target);

    /// <summary>The row's target with environment variables expanded: what the in-app File Explorer is
    /// asked to reveal. Resolved on read, so it follows the session like the runner's does.</summary>
    public string ResolvedPath => ToolkitPaths.Resolve(Action.Target);

    public string BadgeLabel => ToolkitCatalog.LabelFor(Kind);
    public Geometry Icon => ToolkitIcons.GlyphFor(Kind);
    public IBrush BadgeForeground => ToolkitIcons.ForegroundFor(Kind);
    public IBrush BadgeBackground => ToolkitIcons.BackgroundFor(Kind);
}
