namespace DashDetective.Tabs.Toolkit;

/// <summary>
/// Turns a user-authored <see cref="ToolkitCommand"/> into an ordinary <see cref="ToolkitEntry"/>.
///
/// "Ordinary" is the point: the entry it produces goes through the same <see cref="ToolkitAction"/>
/// factories the catalog uses, so <see cref="ToolkitRunner"/> cannot tell a user's row from an authored
/// one and there is no second execution path to keep safe. In particular the arguments arrive as a
/// **list** (via <see cref="ToolkitArgumentParser"/>) and are never joined into a command line, and no
/// mapping here can produce <see cref="ToolkitActionKind.Elevated"/> — <see cref="ToolkitCommandType"/>
/// has no member for it.
/// </summary>
public static class ToolkitCommandFactory {
    /// <summary>The row a user's command becomes. Filed under <see cref="ToolkitCategory.Custom"/>; the
    /// category they picked, if any, rides along on <see cref="ToolkitEntry.SecondaryCategory"/> for the
    /// filter to place it a second time.</summary>
    public static ToolkitEntry ToEntry(ToolkitCommand command) =>
        new(command.Title, command.Description, ToolkitCategory.Custom, KindFor(command.Type),
            ActionFor(command), parameter: null, source: command);

    /// <summary>What running the command does, built through the catalog's own action factories.</summary>
    public static ToolkitAction ActionFor(ToolkitCommand command) {
        var arguments = ToolkitArgumentParser.Split(command.Arguments);
        return command.Type switch {
            ToolkitCommandType.FolderPath => ToolkitAction.OpenPath(command.Payload),
            ToolkitCommandType.Url => ToolkitAction.OpenUrl(command.Payload),
            ToolkitCommandType.Launch => ToolkitAction.Launch(command.Payload, [.. arguments]),
            _ => ToolkitAction.Capture(command.Payload, [.. arguments]),
        };
    }

    /// <summary>The badge a type wears, reusing the existing kinds rather than inventing a "custom"
    /// look: what the row *does* is what its colour should say.</summary>
    public static ToolkitEntryKind KindFor(ToolkitCommandType type) => type switch {
        ToolkitCommandType.FolderPath => ToolkitEntryKind.Folder,
        ToolkitCommandType.Url => ToolkitEntryKind.Link,
        ToolkitCommandType.Launch => ToolkitEntryKind.App,
        _ => ToolkitEntryKind.Command,
    };

    /// <summary>The label a type reads as in the form's type picker.</summary>
    public static string LabelFor(ToolkitCommandType type) => type switch {
        ToolkitCommandType.FolderPath => "Folder path",
        ToolkitCommandType.Url => "URL",
        ToolkitCommandType.Launch => "Launch",
        _ => "Run and capture",
    };
}
