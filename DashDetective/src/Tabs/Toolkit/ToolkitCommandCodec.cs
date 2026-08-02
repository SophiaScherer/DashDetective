using System;
using System.Collections.Generic;
using System.Text;

namespace DashDetective.Tabs.Toolkit;

/// <summary>
/// Encodes the user's own commands as one flat string, so <c>AppSettings</c> needs no knowledge of what a
/// Toolkit command is — <see cref="ToolkitPins"/>' arrangement, one level deeper: ASCII record separator
/// between commands, unit separator between a command's fields.
///
/// Both are control characters, so neither can occur in anything typed into the form, which is what makes
/// joining without escaping safe. Enums are written by name rather than by number, so reordering
/// <see cref="ToolkitCommandType"/> or <see cref="ToolkitCategory"/> cannot silently re-point a stored
/// command at a different meaning.
///
/// Decoding is total: a malformed, truncated or hand-edited record is dropped, never thrown on. A settings
/// file that has been damaged costs its bad rows and nothing else.
/// </summary>
public static class ToolkitCommandCodec {
    private const char CommandSeparator = (char)0x1E;
    private const char FieldSeparator = (char)0x1F;

    /// <summary>How many fields a record carries. A record with any other count is from a schema this
    /// build does not know, and is dropped.</summary>
    private const int FieldCount = 6;

    /// <summary>The commands as one persistable string, in the order given.</summary>
    public static string Encode(IEnumerable<ToolkitCommand> commands) {
        var builder = new StringBuilder();
        foreach (var command in commands) {
            if (string.IsNullOrWhiteSpace(command.Title))
                continue;

            if (builder.Length > 0)
                builder.Append(CommandSeparator);

            builder.Append(command.Title).Append(FieldSeparator)
                   .Append(command.Description).Append(FieldSeparator)
                   .Append(command.Type).Append(FieldSeparator)
                   .Append(command.Payload).Append(FieldSeparator)
                   .Append(command.Arguments).Append(FieldSeparator)
                   .Append(command.Category?.ToString() ?? "");
        }

        return builder.ToString();
    }

    /// <summary>The commands read back, in order, with duplicate titles and unreadable records dropped.
    /// Titles are de-duplicated here as well as refused by <see cref="ToolkitCommandValidator"/>, because
    /// a hand-edited file never went through the form.</summary>
    public static IReadOnlyList<ToolkitCommand> Decode(string? encoded) {
        if (string.IsNullOrEmpty(encoded))
            return [];

        var commands = new List<ToolkitCommand>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var record in encoded.Split(CommandSeparator)) {
            if (Parse(record) is not { } command)
                continue;

            if (seen.Add(command.Title))
                commands.Add(command);
        }

        return commands;
    }

    private static ToolkitCommand? Parse(string record) {
        var fields = record.Split(FieldSeparator);
        if (fields.Length != FieldCount)
            return null;

        var title = fields[0];
        if (string.IsNullOrWhiteSpace(title))
            return null;

        // An unknown type is not guessable — the payload means something different for each — so the
        // record goes rather than being filed under a default that might run the wrong thing.
        if (!Enum.TryParse<ToolkitCommandType>(fields[2], out var type))
            return null;

        // An unknown category, by contrast, only affects where the row is shown, so it degrades to "no
        // second section" and the command survives. "Custom" degrades the same way: a custom row is
        // already in that section, and a hand-edit asking for it twice should not get it twice.
        ToolkitCategory? category =
            Enum.TryParse<ToolkitCategory>(fields[5], out var parsed) &&
            parsed != ToolkitCategory.Custom
                ? parsed
                : null;

        return new ToolkitCommand(title, fields[1], type, fields[3], fields[4], category);
    }
}
