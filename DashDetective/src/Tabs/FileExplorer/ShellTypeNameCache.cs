using System;
using System.Collections.Generic;
using System.IO;

namespace DashDetective.Tabs.FileExplorer;

/// <summary>
/// One folder read's friendly type names, memoized by extension. Both shells derive the name from the
/// path's extension and attributes alone — Windows asks with <c>SHGFI_USEFILEATTRIBUTES</c>, which never
/// opens the file — so one lookup answers every entry sharing an extension, and a folder of 5,000 files
/// costs a few dozen shell round-trips instead of 5,000.
///
/// Deliberately per-read and unshared: nothing to invalidate, and nothing to synchronize.
/// </summary>
internal sealed class ShellTypeNameCache {
    private readonly IShellInterop _shell;
    private readonly Dictionary<string, string> _byExtension = new(StringComparer.OrdinalIgnoreCase);
    private string? _directory;

    internal ShellTypeNameCache(IShellInterop shell) => _shell = shell;

    /// <summary>The shell's name for an entry, asking it only once per extension.</summary>
    internal string NameFor(string fullPath, bool isDirectory) {
        // Under the attributes-only flag every directory answers the same, whatever it is named.
        if (isDirectory)
            return _directory ??= _shell.GetTypeName(fullPath, true);

        var extension = Path.GetExtension(fullPath);
        if (!_byExtension.TryGetValue(extension, out var name))
            _byExtension[extension] = name = _shell.GetTypeName(fullPath, false);

        return name;
    }
}
