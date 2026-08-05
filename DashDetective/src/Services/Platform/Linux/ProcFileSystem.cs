using System;
using System.Collections.Generic;
using System.IO;

namespace DashDetective.Services.Platform.Linux;

/// <summary>
/// The production <see cref="IProcFileSystem"/>: thin <c>System.IO</c> wrappers rooted at <c>/</c>, each
/// swallowing every failure into the empty contract. Stateless, so it is safe to share across the
/// concurrent provider loads.
///
/// Portable managed code, so it carries no <c>[SupportedOSPlatform]</c> — on a host without <c>/proc</c>
/// it simply finds nothing, which is exactly what the Linux providers' callers already handle.
/// </summary>
internal sealed class ProcFileSystem : IProcFileSystem {
    /// <summary>No try/catch: <c>File.Exists</c> and <c>Directory.Exists</c> return false rather than
    /// throwing, for every failure including a denied parent directory.</summary>
    public bool Exists(string path) => File.Exists(path) || Directory.Exists(path);

    public string? ReadAllText(string path) {
        try {
            return File.ReadAllText(path);
        } catch (Exception ex) when (IsIoFailure(ex)) {
            return null;
        }
    }

    public IReadOnlyList<string> ReadAllLines(string path) {
        try {
            return File.ReadAllLines(path);
        } catch (Exception ex) when (IsIoFailure(ex)) {
            return Array.Empty<string>();
        }
    }

    public IReadOnlyList<string> ListDirectory(string path) {
        try {
            var entries = Directory.GetFileSystemEntries(path);
            var names = new string[entries.Length];
            for (var i = 0; i < entries.Length; i++)
                names[i] = Path.GetFileName(entries[i]);

            return names;
        } catch (Exception ex) when (IsIoFailure(ex)) {
            return Array.Empty<string>();
        }
    }

    public string? ResolveLink(string path) {
        try {
            return File.ResolveLinkTarget(path, returnFinalTarget: true)?.FullName;
        } catch (Exception ex) when (IsIoFailure(ex)) {
            return null;
        }
    }

    /// <summary>The failures a pseudo-file read can legitimately produce — missing, denied, malformed or
    /// torn out from under the reader. Used as a <c>catch</c> filter so nothing broader is swallowed.</summary>
    private static bool IsIoFailure(Exception error) =>
        error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException;
}
