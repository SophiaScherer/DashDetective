namespace DashDetective.Tabs.Toolkit;

/// <summary>
/// The kinds of command a user may author for themselves, as the "+ Add command" form offers them.
///
/// Deliberately narrower than <see cref="ToolkitActionKind"/>: there is **no elevated member**, so a
/// user-authored row can never raise a UAC prompt. Elevation stays authored in
/// <see cref="ToolkitCatalog"/>, where it can be reasoned about one row at a time — it is the one
/// privilege boundary the form does not help cross.
/// </summary>
public enum ToolkitCommandType {
    /// <summary>A folder to open, in the app's File Explorer or the machine's.</summary>
    FolderPath,

    /// <summary>A program to start in its own window; nothing is captured.</summary>
    Launch,

    /// <summary>A console command whose output is captured into the Execution Log.</summary>
    Capture,

    /// <summary>An <c>https://</c> address to open in the default browser.</summary>
    Url,
}
