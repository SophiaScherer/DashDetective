using System;

namespace DashDetective.Shared;

/// <summary>
/// How to tell whether two strings name the same path. Windows folds case, Linux does not — so
/// <c>/home/sophia</c> and <c>/home/Sophia</c> are two different folders there, and comparing them
/// case-insensitively would silently merge them in a cache, a dedupe set or a "did we navigate" check.
///
/// This is for path *identity* only. Sorting and filtering names stay
/// <see cref="StringComparison.OrdinalIgnoreCase"/> on every platform — those are presentation, and a
/// user typing "doc" expects to find "Documents" whatever the filesystem thinks.
/// </summary>
internal static class PathComparison {
    /// <summary>The comparison for <c>string.Equals</c> / <c>StartsWith</c> on two paths.</summary>
    internal static readonly StringComparison Comparison =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    /// <summary>The matching comparer, for a path-keyed <c>HashSet</c> or <c>Dictionary</c>.</summary>
    internal static readonly StringComparer Comparer =
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
}
