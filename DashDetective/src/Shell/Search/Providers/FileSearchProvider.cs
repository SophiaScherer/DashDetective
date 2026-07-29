using Avalonia.Media;
using DashDetective.Services.Search;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DashDetective.Shell.Search.Providers;

/// <summary>
/// Finds files and folders, preferring the Windows index and dropping to a live scan when it can't
/// answer. Picking a result opens the File Explorer at the item — into the folder, or at the file's
/// folder with the file selected.
///
/// The two sources are held behind <see cref="IFileSearch"/> rather than being reached for directly, so
/// the fallback rule is one readable line here and each source is replaceable (and, in the scan's case,
/// testable) on its own.
/// </summary>
public sealed class FileSearchProvider : ISearchProvider {
    private readonly IFileSearch _index;
    private readonly IFileSearch _fallback;
    private readonly Func<string?> _currentFolder;
    private readonly Action<string> _reveal;
    private readonly Geometry? _fileIcon;
    private readonly Geometry? _folderIcon;

    /// <param name="index">The fast source; returns null when it cannot answer.</param>
    /// <param name="fallback">The source used when the index cannot answer.</param>
    /// <param name="currentFolder">Where the File Explorer is, which widens the search scope when it is
    /// somewhere outside the user's profile.</param>
    /// <param name="reveal">Opens the File Explorer at a path.</param>
    public FileSearchProvider(
        IFileSearch index, IFileSearch fallback, Func<string?> currentFolder, Action<string> reveal,
        Geometry? fileIcon = null, Geometry? folderIcon = null) {
        _index = index;
        _fallback = fallback;
        _currentFolder = currentFolder;
        _reveal = reveal;
        _fileIcon = fileIcon;
        _folderIcon = folderIcon;
    }

    public SearchCategory Category => SearchCategory.File;

    public async Task<IReadOnlyList<SearchResult>> QueryAsync(SearchQuery query, CancellationToken token) {
        var scopes = SearchScopes.For(_currentFolder());
        if (scopes.Count == 0)
            return [];

        // Ask for more than will be shown: the index orders by modified date, so the best *name* matches
        // are not necessarily its first rows, and ranking needs something to choose between.
        var limit = query.PerCategoryLimit * 4;

        var hits = await _index.SearchAsync(query.Term, scopes, limit, token)
                   ?? await _fallback.SearchAsync(query.Term, scopes, limit, token)
                   ?? [];

        var results = new List<SearchResult>(hits.Count);
        foreach (var hit in hits) {
            if (token.IsCancellationRequested)
                return [];

            var score = SearchRanker.Score(query.Term, hit.Name);
            if (score == SearchRanker.NoMatch)
                continue;

            var path = hit.FullPath;
            results.Add(new SearchResult(
                SearchCategory.File, hit.Name, hit.FolderPath, score,
                () => _reveal(path), hit.IsDirectory ? _folderIcon : _fileIcon, hit.Name));
        }

        return results;
    }
}
