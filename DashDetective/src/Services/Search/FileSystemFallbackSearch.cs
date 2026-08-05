using DashDetective.Shared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DashDetective.Services.Search;

/// <summary>
/// Walks the filesystem for name matches, for when the Windows index can't answer (indexing switched
/// off, still building, or the folder excluded from it).
///
/// It is breadth-first and depth-capped on purpose: a user's matches are overwhelmingly near the top of
/// the folders searched, and going wide before deep means the first few hundred milliseconds turn up the
/// results worth showing rather than descending into one deep <c>node_modules</c>. Both caps and the
/// cancellation token exist because this runs per search and must be abandonable the moment the term
/// changes.
///
/// Enumeration follows <c>DirectoryService</c>'s conventions — hidden and system entries skipped,
/// inaccessible ones ignored, every loop body soft-failing — so an unreadable folder yields a partial
/// list rather than an exception.
/// </summary>
public sealed class FileSystemFallbackSearch : IFileSearch {
    /// <summary>How many folder levels below a scope root to descend. Deep enough to reach
    /// <c>Documents/Projects/Foo</c>, shallow enough not to wander into a dependency tree.</summary>
    internal const int MaxDepth = 4;

    /// <summary>Most folders visited per search, whatever the depth allows. A backstop for a profile
    /// with a pathological number of shallow folders.</summary>
    internal const int MaxFoldersVisited = 2000;

    private static readonly EnumerationOptions Options = new() {
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
    };

    /// <summary>Never null: a scan can always answer, even if the answer is nothing.</summary>
    public Task<IReadOnlyList<FileHit>?> SearchAsync(
        string term, IReadOnlyList<string> scopes, int limit, CancellationToken token) =>
        Task.Run<IReadOnlyList<FileHit>?>(() => Scan(term, scopes, limit, token), token);

    /// <summary>The synchronous core, so the walking rules can be exercised over a real temp tree
    /// without a task or a dispatcher in the way.</summary>
    internal static IReadOnlyList<FileHit> Scan(
        string term, IReadOnlyList<string> scopes, int limit, CancellationToken token) {
        var hits = new List<FileHit>();
        term = term.Trim();
        if (term.Length == 0 || limit <= 0)
            return hits;

        // (folder, depth), oldest first — the breadth-first frontier.
        var queue = new Queue<(string Path, int Depth)>();
        var seen = new HashSet<string>(PathComparison.Comparer);

        foreach (var scope in scopes)
            if (seen.Add(scope))
                queue.Enqueue((scope, 0));

        var visited = 0;
        while (queue.Count > 0 && hits.Count < limit && visited < MaxFoldersVisited) {
            if (token.IsCancellationRequested)
                return hits;

            var (path, depth) = queue.Dequeue();
            visited++;

            ScanFolder(path, depth, term, limit, queue, seen, hits, token);
        }

        return hits;
    }

    private static void ScanFolder(
        string path, int depth, string term, int limit,
        Queue<(string, int)> queue, HashSet<string> seen, List<FileHit> hits, CancellationToken token) {
        try {
            var directory = new DirectoryInfo(path);

            foreach (var sub in directory.EnumerateDirectories("*", Options)) {
                if (token.IsCancellationRequested)
                    return;

                try {
                    if (Matches(sub.Name, term) && hits.Count < limit)
                        hits.Add(new FileHit(sub.Name, sub.FullName, path, true, sub.LastWriteTime));

                    // Queued even once the limit is reached only if we're still under it — a full list
                    // ends the outer loop anyway.
                    if (depth < MaxDepth && seen.Add(sub.FullName))
                        queue.Enqueue((sub.FullName, depth + 1));
                } catch {
                    // Skip an entry that can't be read.
                }
            }

            foreach (var file in directory.EnumerateFiles("*", Options)) {
                if (token.IsCancellationRequested || hits.Count >= limit)
                    return;

                try {
                    if (Matches(file.Name, term))
                        hits.Add(new FileHit(file.Name, file.FullName, path, false, file.LastWriteTime));
                } catch {
                    // Skip an entry that can't be read.
                }
            }
        } catch {
            // Unauthorized / folder gone: keep whatever the rest of the walk finds.
        }
    }

    private static bool Matches(string name, string term) =>
        name.Contains(term, StringComparison.OrdinalIgnoreCase);
}
