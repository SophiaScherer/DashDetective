using System.Collections.Generic;

namespace DashDetective.Shared.Layout;

/// <summary>
/// Reconciles a saved display order against the one the code declares. Extracted from
/// <see cref="WidgetOrders"/> once the Processes table wanted the same semantics for its columns —
/// the arithmetic knows nothing about widgets or columns, only ids.
/// </summary>
public static class OrderResolver {
    /// <summary>
    /// The order to actually show. A saved id the caller no longer declares is dropped; one it has
    /// gained keeps its declared position relative to its neighbours rather than being appended, so
    /// something added in a later release lands where its author put it instead of at the bottom of an
    /// order the user arranged once and forgot.
    /// </summary>
    public static IReadOnlyList<string> Resolve(IReadOnlyList<string> declared, IReadOnlyList<string> saved) {
        if (declared.Count == 0 || saved.Count == 0)
            return declared;

        var rank = new Dictionary<string, double>();
        for (var i = 0; i < saved.Count; i++)
            rank.TryAdd(saved[i], i);

        // An id absent from the save sits just after whichever declared neighbour precedes it, so a
        // stable sort keeps it beside the ids it was authored next to.
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
