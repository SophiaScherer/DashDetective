using System;
using System.Collections.Generic;
using System.Data.OleDb;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DashDetective.Services.Search;

/// <summary>
/// Queries the index Windows already keeps of the user's files, through the <c>Search.CollatorDSO</c>
/// OLE DB provider — the same index File Explorer's own search box uses.
///
/// This is why universal search can answer across a whole user profile instantly: the alternative,
/// walking the filesystem per keystroke, is what <see cref="FileSystemFallbackSearch"/> does and it is
/// visibly slower. The index is not always there, though — it can be switched off, excluded for a
/// folder, or still building — so a failure here is reported as "unavailable" (a null result) rather
/// than "nothing found", and the provider drops to the scan.
///
/// Soft-failing throughout, following the house provider convention: no query the user types can throw
/// out of here.
/// </summary>
public sealed class WindowsSearchIndex : IFileSearch {
    private const string ConnectionString =
        "Provider=Search.CollatorDSO;Extended Properties=\"Application=Windows\"";

    // System.ItemType is the extension for a file and the literal "Directory" for a folder.
    private const string DirectoryItemType = "Directory";

    public Task<IReadOnlyList<FileHit>?> SearchAsync(
        string term, IReadOnlyList<string> scopes, int limit, CancellationToken token) {
        if (SearchTermEscaper.Escape(term) is not { } escaped || scopes.Count == 0)
            return Task.FromResult<IReadOnlyList<FileHit>?>(null);

        var sql = BuildQuery(escaped, scopes, limit);
        return Task.Run<IReadOnlyList<FileHit>?>(() => Run(sql, limit, token), token);
    }

    /// <summary>
    /// Builds the query. Ordering by modified date descending is what makes the results feel relevant:
    /// a term matches a great many files in a home directory, and the ones worth showing are the ones
    /// the user has touched recently.
    /// </summary>
    internal static string BuildQuery(string escapedTerm, IReadOnlyList<string> scopes, int limit) {
        var sql = new StringBuilder();
        sql.Append("SELECT TOP ").Append(limit.ToString(CultureInfo.InvariantCulture)).Append(' ')
           .Append("System.ItemNameDisplay, System.ItemPathDisplay, ")
           .Append("System.ItemFolderPathDisplay, System.ItemType, System.DateModified ")
           .Append("FROM SystemIndex WHERE (");

        for (var i = 0; i < scopes.Count; i++) {
            if (i > 0)
                sql.Append(" OR ");
            sql.Append("SCOPE='file:").Append(SearchTermEscaper.EscapeScope(scopes[i])).Append('\'');
        }

        // CONTAINS over System.FileName is the index-accelerated path; a LIKE over the same column is
        // not, and takes seconds rather than milliseconds on a full profile.
        sql.Append(") AND CONTAINS(System.FileName, '\"").Append(escapedTerm).Append("*\"') ")
           .Append("ORDER BY System.DateModified DESC");

        return sql.ToString();
    }

    private static IReadOnlyList<FileHit>? Run(string sql, int limit, CancellationToken token) {
        if (!OperatingSystem.IsWindows())
            return null;

        var hits = new List<FileHit>(limit);
        try {
            using var connection = new OleDbConnection(ConnectionString);
            connection.Open();

            using var command = new OleDbCommand(sql, connection);
            using var reader = command.ExecuteReader();

            while (reader.Read()) {
                // The user types faster than the index answers; abandon a superseded query mid-read
                // rather than paying for rows nobody will see.
                if (token.IsCancellationRequested)
                    return null;

                if (ReadHit(reader) is { } hit)
                    hits.Add(hit);
            }
        } catch {
            // Indexing switched off, the provider missing, the service stopped, a malformed clause:
            // all mean the same thing to the caller — ask somewhere else.
            return null;
        }

        return hits;
    }

    // One row, defensively: the index can hold an entry whose columns are null (a file removed since it
    // was indexed), and one such row must not cost the whole result set.
    private static FileHit? ReadHit(OleDbDataReader reader) {
        try {
            if (reader.GetValue(1) is not string fullPath || fullPath.Length == 0)
                return null;

            var name = reader.GetValue(0) as string ?? "";
            var folder = reader.GetValue(2) as string ?? "";
            var itemType = reader.GetValue(3) as string ?? "";
            var modified = reader.GetValue(4) as DateTime? ?? DateTime.MinValue;

            return new FileHit(
                name, fullPath, folder,
                string.Equals(itemType, DirectoryItemType, StringComparison.OrdinalIgnoreCase),
                modified);
        } catch {
            return null;
        }
    }
}
