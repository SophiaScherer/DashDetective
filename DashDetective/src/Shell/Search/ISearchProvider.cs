using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DashDetective.Shell.Search;

/// <summary>
/// One source of search results. Providers are independent and are queried concurrently, so each owns
/// exactly one category and knows nothing about the others.
///
/// Implementations follow the house provider convention: never throw, and soft-fail to an empty list —
/// a Windows index that isn't running must cost the user their file results, not their whole search.
/// </summary>
public interface ISearchProvider {
    /// <summary>The category every result from this provider carries.</summary>
    SearchCategory Category { get; }

    /// <summary>Finds up to <see cref="SearchQuery.PerCategoryLimit"/> matches. Long-running providers
    /// must honour <paramref name="token"/>: the aggregator cancels a query the moment the user types
    /// again.</summary>
    Task<IReadOnlyList<SearchResult>> QueryAsync(SearchQuery query, CancellationToken token);
}
