namespace DashDetective.Tabs.Toolkit;

/// <summary>
/// What a command opens, which decides its row icon and badge. Orthogonal to
/// <see cref="ToolkitCategory"/>: a folder shortcut and a control panel can share a section.
/// </summary>
public enum ToolkitEntryKind {
    Folder,
    App,
    Command,
    Panel,
}
