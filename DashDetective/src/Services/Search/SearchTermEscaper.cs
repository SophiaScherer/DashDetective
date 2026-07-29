using System.Text;

namespace DashDetective.Services.Search;

/// <summary>
/// Makes a typed term safe to paste into a Windows Search SQL statement.
///
/// The <c>Search.CollatorDSO</c> provider does not bind parameters inside <c>CONTAINS</c>, so the term
/// has to be inlined — which means the escaping is the only thing standing between a keystroke and a
/// malformed (or differently-meaning) query. It is a pure static, kept apart from the query it feeds,
/// precisely so that rule is testable on its own.
///
/// The term lands inside a SQL string literal that itself contains a quoted phrase:
/// <c>CONTAINS(System.FileName, '"term*"')</c>. So double quotes would end the phrase early, single
/// quotes would end the literal, and the wildcards are ours to add rather than the user's to inject.
/// </summary>
public static class SearchTermEscaper {
    /// <summary>Longest term passed through. A filename search this long is a paste, not a search, and
    /// the tokenizer has limits of its own.</summary>
    private const int MaxLength = 64;

    /// <summary>
    /// Returns the term ready to inline, or <c>null</c> when nothing usable survives — the caller skips
    /// the query rather than sending one that matches everything.
    /// </summary>
    public static string? Escape(string? term) {
        if (string.IsNullOrWhiteSpace(term))
            return null;

        term = term.Trim();
        if (term.Length > MaxLength)
            term = term[..MaxLength];

        var escaped = new StringBuilder(term.Length + 4);
        foreach (var c in term) {
            switch (c) {
                // Would close the phrase, close the literal, or add a wildcard of the user's own.
                case '"' or '*' or '?' or '\\':
                    break;

                // Doubling is how a SQL string literal carries a quote.
                case '\'':
                    escaped.Append("''");
                    break;

                default:
                    if (!char.IsControl(c))
                        escaped.Append(c);
                    break;
            }
        }

        var result = escaped.ToString().Trim();
        return result.Length == 0 ? null : result;
    }

    /// <summary>Escapes a folder path for a <c>SCOPE</c> clause. Paths can legally contain a single
    /// quote, which would otherwise close the literal early.</summary>
    public static string EscapeScope(string path) => path.Replace("'", "''");
}
