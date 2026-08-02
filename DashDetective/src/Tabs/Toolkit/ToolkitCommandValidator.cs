using System;
using System.Collections.Generic;

namespace DashDetective.Tabs.Toolkit;

/// <summary>
/// Whether a user-authored command is one the page will accept, and what to say when it is not. Pure
/// statics, like <see cref="ToolkitHostValidator"/>.
///
/// Two of these rules are correctness and one is safety. Correctness: a row needs a label and a payload,
/// and its title has to be unique — pins and search reveal are keyed by command text, so two rows sharing
/// one would make both ambiguous. Safety: a URL must be <c>https://</c>. The runner refuses anything else
/// regardless (see <see cref="ToolkitRunner.RunAsync"/>), so this is not the boundary — it is the same
/// answer given at the point where it is still fixable, rather than as a failed run.
/// </summary>
public static class ToolkitCommandValidator {
    public const string TitleRequired = "Give the command a title.";
    public const string TitleTaken = "A command with that title already exists.";
    public const string PathRequired = "Give the folder a path.";
    public const string ProgramRequired = "Name the program to run.";
    public const string UrlRequired = "Give the link an address.";
    public const string UrlMustBeHttps = "Links must start with https://.";
    public const string PathCannotBeUrl = "That looks like a link — pick the URL type instead.";

    /// <summary>The reason the command is refused, or <c>null</c> when it is fine.</summary>
    /// <param name="command">The command as typed.</param>
    /// <param name="existing">Every row already on the page, for the uniqueness check.</param>
    /// <param name="replacing">The command being edited, whose own title does not count as a clash.</param>
    public static string? Validate(
        ToolkitCommand command, IEnumerable<ToolkitEntry> existing, ToolkitCommand? replacing = null) {
        var title = command.Title.Trim();
        if (title.Length == 0)
            return TitleRequired;

        foreach (var entry in existing) {
            if (!string.Equals(entry.Command, title, StringComparison.OrdinalIgnoreCase))
                continue;

            // Editing a row without renaming it is not a clash with itself.
            if (replacing is not null && ReferenceEquals(entry.Source, replacing))
                continue;

            return TitleTaken;
        }

        return ValidatePayload(command);
    }

    private static string? ValidatePayload(ToolkitCommand command) {
        var payload = command.Payload.Trim();

        switch (command.Type) {
            case ToolkitCommandType.Url:
                if (payload.Length == 0)
                    return UrlRequired;
                return payload.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                    ? null
                    : UrlMustBeHttps;

            case ToolkitCommandType.FolderPath:
                if (payload.Length == 0)
                    return PathRequired;

                // Caught here because OpenPath hands the target to the shell, which would happily open a
                // URL from a row badged as a folder — the label would be lying about what it does.
                return LooksLikeUrl(payload) ? PathCannotBeUrl : null;

            default:
                return payload.Length == 0 ? ProgramRequired : null;
        }
    }

    private static bool LooksLikeUrl(string payload) =>
        payload.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
        payload.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

    /// <summary>The command with its free text trimmed — what is stored once
    /// <see cref="Validate"/> has passed, so a stray space cannot make two titles look different.</summary>
    public static ToolkitCommand Normalize(ToolkitCommand command) => command with {
        Title = command.Title.Trim(),
        Description = command.Description.Trim(),
        Payload = command.Payload.Trim(),
        Arguments = command.Arguments.Trim(),
    };
}
