using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DashDetective.Services.Search;

/// <summary>
/// A source of file and folder matches. Two implement it — the Windows index and a live scan — so the
/// provider that uses them can prefer the fast one and drop to the slow one without knowing how either
/// works.
/// </summary>
public interface IFileSearch {
    /// <summary>
    /// Finds up to <paramref name="limit"/> matches for <paramref name="term"/> under
    /// <paramref name="scopes"/>.
    ///
    /// Returns <c>null</c> when this source cannot answer at all — indexing is switched off, the
    /// provider isn't installed — which is different from an empty list meaning "nothing matched".
    /// Only the first is worth falling back from; the second is the honest answer.
    /// </summary>
    Task<IReadOnlyList<FileHit>?> SearchAsync(
        string term, IReadOnlyList<string> scopes, int limit, CancellationToken token);
}
