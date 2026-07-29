using System;
using System.Collections.Generic;

namespace DashDetective.Shared.Completion;

/// <summary>
/// Works out what a half-typed word should complete to. A pure static like <c>ProcessFilter</c>, shared
/// by every field that ghosts a suggestion — the search box, the folder path bar and the process filter.
///
/// One match completes to the whole thing. Several complete only as far as they agree, which is the
/// shell convention and the honest one: with <c>Documents</c> and <c>Downloads</c> both on offer,
/// "Do" completes to "Do" (no suggestion) rather than picking one and being wrong half the time.
/// </summary>
public static class PrefixCompleter {
    /// <summary>
    /// The full completion for <paramref name="typed"/>, or <c>null</c> when there is nothing to add —
    /// nothing matches, or what matches adds no characters (the word is already fully typed).
    ///
    /// The result carries the candidates' own casing, so a caller wanting to preserve what the user
    /// typed should append only the part past <paramref name="typed"/>'s length.
    /// </summary>
    public static string? Complete(string? typed, IEnumerable<string> candidates) {
        if (string.IsNullOrEmpty(typed))
            return null;

        string? agreed = null;
        foreach (var candidate in candidates) {
            if (string.IsNullOrEmpty(candidate) ||
                !candidate.StartsWith(typed, StringComparison.OrdinalIgnoreCase))
                continue;

            agreed = agreed is null ? candidate : SharedPrefix(agreed, candidate);

            // Once the candidates agree on nothing beyond what is already typed, no later one can add
            // anything back.
            if (agreed.Length <= typed.Length)
                return null;
        }

        return agreed is not null && agreed.Length > typed.Length ? agreed : null;
    }

    /// <summary>The suffix a ghost should draw after what the user has typed, or "" for no suggestion.
    /// Convenience over <see cref="Complete"/> for the common case of rendering one.</summary>
    public static string Suffix(string? typed, string? completion) {
        // An empty box ghosts nothing, matching Complete: a suggestion before the first keystroke would
        // be a guess at what the user is about to want.
        if (string.IsNullOrEmpty(typed) || completion is null || completion.Length <= typed.Length)
            return "";

        return completion.StartsWith(typed, StringComparison.OrdinalIgnoreCase)
            ? completion[typed.Length..]
            : "";
    }

    // The longest start the two share, compared case-insensitively but returned in the first one's
    // casing (either is as good; the caller only uses the length past what was typed).
    private static string SharedPrefix(string a, string b) {
        var max = Math.Min(a.Length, b.Length);
        var shared = 0;
        while (shared < max && char.ToUpperInvariant(a[shared]) == char.ToUpperInvariant(b[shared]))
            shared++;

        return a[..shared];
    }
}
