using System.Collections.Generic;
using System.Text;

namespace DashDetective.Shared.Layout;

/// <summary>
/// Encodes each page's widget order as one flat string, so <c>AppSettings</c> needs no knowledge of
/// what a widget is — the encoding lives next to the thing it encodes, as <c>ToolkitPins</c> does.
///
/// Order is stored by widget id, never by index: a page that gains or loses a widget between releases
/// must not silently re-point a saved layout at different widgets.
/// </summary>
public static class WidgetOrders {
    // ASCII record and unit separators. Control characters, so they cannot occur in a page key or a
    // widget id, which is what makes joining without escaping safe.
    private const char PageSeparator = (char)0x1E;
    private const char FieldSeparator = (char)0x1F;

    /// <summary>Every page's order as one persistable string.</summary>
    public static string Encode(IReadOnlyDictionary<string, IReadOnlyList<string>> orders) {
        var builder = new StringBuilder();
        foreach (var (page, ids) in orders) {
            if (string.IsNullOrWhiteSpace(page) || ids.Count == 0)
                continue;

            if (builder.Length > 0)
                builder.Append(PageSeparator);

            builder.Append(page);
            var seen = new HashSet<string>();
            foreach (var id in ids)
                if (!string.IsNullOrWhiteSpace(id) && seen.Add(id))
                    builder.Append(FieldSeparator).Append(id);
        }

        return builder.ToString();
    }

    /// <summary>The saved orders read back. Total: a malformed, truncated or hand-edited record is
    /// dropped rather than thrown on, so a bad settings file costs its bad records and nothing more.</summary>
    public static Dictionary<string, IReadOnlyList<string>> Decode(string? encoded) {
        var orders = new Dictionary<string, IReadOnlyList<string>>();
        if (string.IsNullOrEmpty(encoded))
            return orders;

        foreach (var record in encoded.Split(PageSeparator)) {
            var fields = record.Split(FieldSeparator);
            if (fields.Length < 2 || string.IsNullOrWhiteSpace(fields[0]))
                continue;

            var ids = new List<string>();
            var seen = new HashSet<string>();
            for (var i = 1; i < fields.Length; i++)
                if (!string.IsNullOrWhiteSpace(fields[i]) && seen.Add(fields[i]))
                    ids.Add(fields[i]);

            if (ids.Count > 0)
                orders[fields[0]] = ids;
        }

        return orders;
    }

    /// <summary>The order to actually show, given what the page declares now and what was saved. The
    /// arithmetic is <see cref="OrderResolver"/>'s — the Processes table's columns resolve the same
    /// way.</summary>
    public static IReadOnlyList<string> Resolve(IReadOnlyList<string> declared, IReadOnlyList<string> saved) =>
        OrderResolver.Resolve(declared, saved);
}
