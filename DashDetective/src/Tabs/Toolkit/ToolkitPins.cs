using System.Collections.Generic;
using System.Text;

namespace DashDetective.Tabs.Toolkit;

/// <summary>
/// Encodes which commands the user has pinned, as one flat string, so <c>AppSettings</c> needs no
/// knowledge of what a Toolkit command is — the encoding lives next to the thing it encodes, exactly as
/// <c>RecentSearches</c> does for the search history.
///
/// Pins are stored by **command text**, not by index: a catalog that gains or loses a row between
/// sessions must not silently re-point somebody's pins at different commands. A pin naming a command
/// that no longer exists is simply dropped when it is applied.
/// </summary>
public static class ToolkitPins {
    // ASCII record separator — a control character, so it cannot occur in a command, which is what makes
    // joining without escaping safe. Same reasoning as RecentSearches.
    private const char EntrySeparator = (char)0x1E;

    /// <summary>The pinned commands as one persistable string, in the order given.</summary>
    public static string Encode(IEnumerable<string> commands) {
        var builder = new StringBuilder();
        foreach (var command in commands) {
            if (string.IsNullOrWhiteSpace(command))
                continue;

            if (builder.Length > 0)
                builder.Append(EntrySeparator);

            builder.Append(command);
        }

        return builder.ToString();
    }

    /// <summary>The pinned commands read back, de-duplicated and in order. Anything blank is skipped, so
    /// a hand-edited or older settings file costs its bad entries and nothing more.</summary>
    public static IReadOnlyList<string> Decode(string? encoded) {
        if (string.IsNullOrEmpty(encoded))
            return [];

        var commands = new List<string>();
        var seen = new HashSet<string>();
        foreach (var command in encoded.Split(EntrySeparator))
            if (!string.IsNullOrWhiteSpace(command) && seen.Add(command))
                commands.Add(command);

        return commands;
    }
}
