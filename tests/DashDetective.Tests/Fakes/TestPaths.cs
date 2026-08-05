using System;
using System.IO;

namespace DashDetective.Tests.Fakes;

/// <summary>
/// Builds rooted paths shaped like the running platform's, for the tests whose subject actually calls
/// <c>Path.*</c>. A literal <c>@"C:\Users\Sophia"</c> is not a path on Linux — <c>\</c> is an ordinary
/// filename character there, so it splits into no segments and <c>Path.GetFullPath</c> resolves it
/// against the working directory.
///
/// Only for tests that need a *real* path. Where a path is just an opaque token being round-tripped
/// (<c>NavigationHistory</c>, <c>RecentSearches</c>), the drive-letter literals are clearer and stay.
/// </summary>
internal static class TestPaths {
    private static readonly char Sep = Path.DirectorySeparatorChar;

    /// <summary>The filesystem root: <c>C:\</c> or <c>/</c>.</summary>
    internal static string Root { get; } = OperatingSystem.IsWindows() ? @"C:\" : "/";

    /// <summary>A rooted path with no trailing separator — <c>Of("Users", "Sophia")</c> is
    /// <c>C:\Users\Sophia</c> or <c>/Users/Sophia</c>.</summary>
    internal static string Of(params string[] segments) => Root + string.Join(Sep, segments);

    /// <summary>The same, ending in a separator, for the callers that care about one (a completion
    /// parent, a folder key). <c>Dir()</c> is the root itself.</summary>
    internal static string Dir(params string[] segments) =>
        segments.Length == 0 ? Root : Of(segments) + Sep;
}
