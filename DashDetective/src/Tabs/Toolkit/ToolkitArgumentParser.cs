using System.Collections.Generic;
using System.Text;

namespace DashDetective.Tabs.Toolkit;

/// <summary>
/// Splits one typed argument string into the separate arguments the OS is given. Pure statics, like
/// <see cref="ToolkitHostValidator"/>.
///
/// This is a **split, not a parse**: whitespace separates, double quotes group (so a path with spaces
/// survives), and nothing else is interpreted. There is no escaping, no variable substitution and no
/// shell metacharacter handling, because what comes out of here goes straight into
/// <c>ProcessStartInfo.ArgumentList</c> — <c>&amp;</c>, <c>|</c> and <c>&gt;</c> reach the program as
/// literal text, since no shell is ever involved (see <see cref="SystemProcessLauncher"/>).
/// </summary>
public static class ToolkitArgumentParser {
    /// <summary>The arguments, in order. A blank string yields none.</summary>
    public static IReadOnlyList<string> Split(string? arguments) {
        if (string.IsNullOrWhiteSpace(arguments))
            return [];

        var parts = new List<string>();
        var current = new StringBuilder();
        var quoted = false;

        // Tracked separately from the builder's length so an explicitly empty argument ("") is kept,
        // while a run of spaces between arguments produces nothing.
        var started = false;

        foreach (var c in arguments) {
            if (c == '"') {
                quoted = !quoted;
                started = true;
                continue;
            }

            if (!quoted && char.IsWhiteSpace(c)) {
                if (started) {
                    parts.Add(current.ToString());
                    current.Clear();
                    started = false;
                }

                continue;
            }

            current.Append(c);
            started = true;
        }

        // An unclosed quote takes the rest of the line as one argument rather than being an error: the
        // user is mid-thought, and refusing to split is friendlier than refusing to run.
        if (started)
            parts.Add(current.ToString());

        return parts;
    }
}
