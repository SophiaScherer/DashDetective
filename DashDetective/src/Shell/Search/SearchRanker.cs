using System;

namespace DashDetective.Shell.Search;

/// <summary>
/// Scores how well a term matches a piece of text, so results from unrelated providers can be merged
/// into one ordered list. A pure static like <c>ProcessFilter</c> — no I/O, no control types — so the
/// ordering rules are testable on their own.
///
/// Matches fall into four tiers (exact, prefix, word-start, anywhere), each 200 apart. Within a tier,
/// results are separated by how much of the text the term covers, so "CPU" ranks "CPU" above
/// "CPU usage history". That closeness bonus is capped below 100, which keeps it strictly inside its
/// tier: a word-start match can never overtake a prefix match however short it is.
/// </summary>
public static class SearchRanker {
    /// <summary>The score of a text the term doesn't appear in at all.</summary>
    public const int NoMatch = 0;

    private const int Exact = 1000;
    private const int Prefix = 800;
    private const int WordStart = 600;
    private const int Anywhere = 400;

    /// <summary>The most the closeness bonus can add. Strictly less than the 200-point tier gap.</summary>
    private const int MaxCloseness = 99;

    /// <summary>The penalty each field past the first takes in <see cref="ScoreBest"/>. Capped below the
    /// closeness bonus so demoting a field can never push it out of its tier.</summary>
    private const int FieldPenalty = 25;
    private const int MaxFieldPenalty = 75;

    // Characters that end a word, so "usage" is a word-start match inside "CPU usage" and inside
    // "high-usage" but not inside "misusage".
    private static readonly char[] WordBreaks = [' ', '-', '_', '.', ',', '/', '\\', '(', ')', ':'];

    /// <summary>Scores <paramref name="term"/> against one piece of text, or <see cref="NoMatch"/> when
    /// it doesn't appear. An empty term matches nothing — the caller decides what an empty box shows.</summary>
    public static int Score(string term, string? text) {
        if (string.IsNullOrEmpty(text))
            return NoMatch;

        term = term.Trim();
        if (term.Length == 0 || term.Length > text.Length)
            return NoMatch;

        var tier = Tier(term, text);
        return tier == NoMatch ? NoMatch : tier + Closeness(term.Length, text.Length);
    }

    /// <summary>
    /// Scores <paramref name="term"/> against several fields and keeps the best, demoting later fields
    /// slightly so a title match outranks an equally strong match buried in a description.
    /// </summary>
    public static int ScoreBest(string term, params string?[] fields) {
        var best = NoMatch;

        for (var i = 0; i < fields.Length; i++) {
            var score = Score(term, fields[i]);
            if (score == NoMatch)
                continue;

            var penalty = Math.Min(i * FieldPenalty, MaxFieldPenalty);
            best = Math.Max(best, Math.Max(score - penalty, 1));
        }

        return best;
    }

    private static int Tier(string term, string text) {
        if (text.Equals(term, StringComparison.OrdinalIgnoreCase))
            return Exact;
        if (text.StartsWith(term, StringComparison.OrdinalIgnoreCase))
            return Prefix;
        if (StartsAWord(text, term))
            return WordStart;
        if (text.Contains(term, StringComparison.OrdinalIgnoreCase))
            return Anywhere;

        return NoMatch;
    }

    // True when the term begins a word somewhere inside the text (the start of the text is covered by
    // the prefix tier above, so only positions after a word break are considered here).
    private static bool StartsAWord(string text, string term) {
        for (var i = 0; i < text.Length - 1; i++) {
            if (Array.IndexOf(WordBreaks, text[i]) < 0)
                continue;

            var start = i + 1;
            if (text.Length - start >= term.Length &&
                string.Compare(text, start, term, 0, term.Length, StringComparison.OrdinalIgnoreCase) == 0)
                return true;
        }

        return false;
    }

    // How much of the text the term accounts for, as 0..MaxCloseness. A term covering the whole text
    // scores the maximum; one buried in a long name scores near zero.
    private static int Closeness(int termLength, int textLength) =>
        textLength == 0 ? 0 : MaxCloseness * termLength / textLength;
}
