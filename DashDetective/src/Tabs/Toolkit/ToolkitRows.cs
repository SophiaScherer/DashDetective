namespace DashDetective.Tabs.Toolkit;

/// <summary>
/// The shapes a built-in row comes in, shared by every platform's catalog. Each factory pairs one
/// <see cref="ToolkitCategory"/> with one <see cref="ToolkitEntryKind"/> and one
/// <see cref="ToolkitAction"/> kind, so the tables cannot drift apart on the only thing they could
/// meaningfully disagree about — what a "folder row" or a "diagnostic row" actually is.
/// </summary>
internal static class ToolkitRows {
    /// <summary>A row that opens a folder in the desktop file manager — the shape every Folders entry
    /// takes.</summary>
    internal static ToolkitEntry Folder(string path, string description) =>
        new(path, description, ToolkitCategory.Folders, ToolkitEntryKind.Folder,
            ToolkitAction.OpenPath(path));

    /// <summary>A standalone tool or console. Launched through the shell rather than resolved to a
    /// full path, so associations and the PATH apply — the same way typing the name into a run box
    /// does.</summary>
    internal static ToolkitEntry Tool(string command, string description) =>
        new(command, description, ToolkitCategory.SystemTools, ToolkitEntryKind.App,
            ToolkitAction.Launch(command));

    /// <summary>A settings applet. Same launch path as <see cref="Tool"/>; the separate kind is what
    /// gives it its own badge and colour.</summary>
    internal static ToolkitEntry Panel(string command, string description) =>
        new(command, description, ToolkitCategory.SystemTools, ToolkitEntryKind.Panel,
            ToolkitAction.Launch(command));

    /// <summary>A console command whose output is captured into the Execution Log. The action is passed
    /// in rather than derived from the label: the row reads as one command line, but the runner needs it
    /// already split into a file name and separate arguments.</summary>
    internal static ToolkitEntry Diagnostic(
        string command, string description, ToolkitAction action, ToolkitParameter? parameter = null) =>
        new(command, description, ToolkitCategory.Diagnostics, ToolkitEntryKind.Command, action,
            parameter);

    /// <summary>A documentation link. Labelled by title rather than URL — a reference URL would
    /// ellipsize to nothing in the row's mono label. The URL still shows in the Execution Log's "$"
    /// line, so what was opened is on the record.</summary>
    internal static ToolkitEntry Doc(string title, string description, string url) =>
        new(title, description, ToolkitCategory.DocsAndLinks, ToolkitEntryKind.Link,
            ToolkitAction.OpenUrl(url));
}
