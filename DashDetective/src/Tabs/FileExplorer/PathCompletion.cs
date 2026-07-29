using DashDetective.Shared.Completion;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace DashDetective.Tabs.FileExplorer;

/// <summary>
/// Completes the folder being typed in the address bar, the way Windows Explorer's own does: type
/// <c>C:\Us</c> and <c>ers</c> ghosts in after the caret.
///
/// Only the last segment is ever completed, so the folder to read is whatever the path names up to the
/// final separator. That folder's children are cached, because a user typing a name generates a
/// keystroke per character against the same parent and re-enumerating it each time would be pure waste.
/// The cache is one entry deep — moving to a different parent replaces it — which is all that is needed
/// when the caller is one text box with one caret.
/// </summary>
public sealed class PathCompletion {
    private readonly Func<string, bool, Task<IReadOnlyList<DirEntry>>> _readSubdirectories;

    private string _cachedParent = "";
    private List<string> _cachedNames = new();

    public PathCompletion() : this(DirectoryService.GetSubdirectoriesAsync) { }

    /// <summary>Test seam: takes the directory read explicitly, so the completion rules can be
    /// exercised without a filesystem.</summary>
    internal PathCompletion(Func<string, bool, Task<IReadOnlyList<DirEntry>>> readSubdirectories) =>
        _readSubdirectories = readSubdirectories;

    /// <summary>
    /// The full path the typed text should complete to, or <c>null</c> when there is nothing to suggest.
    /// Soft-failing: an unreadable or nonexistent parent simply yields no suggestion.
    /// </summary>
    public async Task<string?> CompleteAsync(string? typed, bool includeHidden) {
        if (!TrySplit(typed, out var parent, out var stub))
            return null;

        var names = await NamesAsync(parent, includeHidden);
        return PrefixCompleter.Complete(stub, names) is { } completed ? parent + completed : null;
    }

    /// <summary>
    /// Splits typed text into the folder to look in and the partial name to complete. Returns false when
    /// there is nothing to complete: no separator (a bare relative name, which names no folder to read)
    /// or nothing typed after the last one.
    /// </summary>
    internal static bool TrySplit(string? typed, out string parent, out string stub) {
        parent = "";
        stub = "";

        if (string.IsNullOrEmpty(typed))
            return false;

        var split = typed.LastIndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]);
        if (split < 0)
            return false;

        // The separator stays with the parent, so re-joining is a plain concatenation.
        parent = typed[..(split + 1)];
        stub = typed[(split + 1)..];

        return stub.Length > 0;
    }

    private async Task<IReadOnlyList<string>> NamesAsync(string parent, bool includeHidden) {
        if (string.Equals(parent, _cachedParent, StringComparison.OrdinalIgnoreCase))
            return _cachedNames;

        List<string> names = new();
        try {
            foreach (var entry in await _readSubdirectories(parent, includeHidden))
                names.Add(entry.Name);
        } catch {
            // Unreadable, gone, or a malformed path: no suggestion rather than an exception per keystroke.
            names.Clear();
        }

        _cachedParent = parent;
        _cachedNames = names;
        return names;
    }
}
