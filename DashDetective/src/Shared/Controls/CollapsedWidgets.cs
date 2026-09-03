using System.Collections.Generic;
using System.Text;

namespace DashDetective.Shared.Controls;

/// <summary>
/// Encodes which widgets are folded shut as one flat string, so <c>AppSettings</c> needs no knowledge
/// of what a widget is — the encoding lives next to the thing it encodes, as <c>WidgetOrders</c> does.
///
/// Stored by widget id, never by index: a page that gains or loses a widget between releases must not
/// silently fold a different one.
/// </summary>
public static class CollapsedWidgets {
    // ASCII unit separator. A control character, so it cannot occur in a widget id, which is what
    // makes joining without escaping safe.
    private const char FieldSeparator = (char)0x1F;

    /// <summary>The folded ids as one persistable string. Blanks and repeats are dropped.</summary>
    public static string Encode(IEnumerable<string> ids) {
        var builder = new StringBuilder();
        var seen = new HashSet<string>();
        foreach (var id in ids) {
            if (string.IsNullOrWhiteSpace(id) || !seen.Add(id))
                continue;

            if (builder.Length > 0)
                builder.Append(FieldSeparator);
            builder.Append(id);
        }

        return builder.ToString();
    }

    /// <summary>The folded ids read back. Total: a malformed or hand-edited record costs its bad
    /// entries and nothing more.</summary>
    public static IReadOnlyList<string> Decode(string? encoded) {
        var ids = new List<string>();
        if (string.IsNullOrEmpty(encoded))
            return ids;

        var seen = new HashSet<string>();
        foreach (var field in encoded.Split(FieldSeparator))
            if (!string.IsNullOrWhiteSpace(field) && seen.Add(field))
                ids.Add(field);

        return ids;
    }
}
