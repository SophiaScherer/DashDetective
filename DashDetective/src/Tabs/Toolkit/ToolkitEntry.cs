using Avalonia.Media;

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
public sealed class ToolkitEntry {
    public ToolkitEntry(
        string command, string description, ToolkitCategory category, ToolkitEntryKind kind,
        ToolkitAction action) {
        Command = command;
        Description = description;
        Category = category;
        Kind = kind;
        Action = action;
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

    public string BadgeLabel => ToolkitCatalog.LabelFor(Kind);
    public Geometry Icon => ToolkitIcons.GlyphFor(Kind);
    public IBrush BadgeForeground => ToolkitIcons.ForegroundFor(Kind);
    public IBrush BadgeBackground => ToolkitIcons.BackgroundFor(Kind);
}
