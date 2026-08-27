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

    /// <summary>
    /// The order to actually show, given what the page declares now and what was saved. A saved id the
    /// page no longer has is dropped; one the page has gained keeps its declared position relative to
    /// its neighbours rather than being appended, so a widget added in a later release lands where its
    /// author put it instead of at the bottom of a layout the user arranged once and forgot.
    /// </summary>
    public static IReadOnlyList<string> Resolve(IReadOnlyList<string> declared, IReadOnlyList<string> saved) {
        if (declared.Count == 0 || saved.Count == 0)
            return declared;

        var rank = new Dictionary<string, double>();
        for (var i = 0; i < saved.Count; i++)
            rank.TryAdd(saved[i], i);

        // An id absent from the save sits just after whichever declared neighbour precedes it, so a
        // stable sort keeps it beside the widgets it was authored next to.
        var ranked = new List<(string Id, double Rank, int Declared)>(declared.Count);
        var previous = -1.0;
        var epsilon = 0.0;
        var seen = new HashSet<string>();

        for (var i = 0; i < declared.Count; i++) {
            var id = declared[i];
            if (string.IsNullOrWhiteSpace(id) || !seen.Add(id))
                continue;

            if (rank.TryGetValue(id, out var savedRank)) {
                previous = savedRank;
                epsilon = 0;
                ranked.Add((id, savedRank, i));
            } else {
                epsilon += 1e-3;
                ranked.Add((id, previous + epsilon, i));
            }
        }

        ranked.Sort((a, b) => a.Rank != b.Rank ? a.Rank.CompareTo(b.Rank) : a.Declared.CompareTo(b.Declared));
        return ranked.ConvertAll(entry => entry.Id);
    }
}
