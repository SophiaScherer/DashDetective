using System.Collections.Generic;

namespace DashDetective.Services.Platform.Linux;

/// <summary>
/// Reads the Linux pseudo-filesystems (<c>/proc</c>, <c>/sys</c>) behind a seam, so every Linux provider
/// can be unit-tested from a Windows dev box against canned fixtures. Infrastructure rather than a
/// provider seam — the same shape and placement as <c>Services/Threading/IUiTimer</c>, which is why it
/// lives in its own <c>Services</c> folder rather than a tab folder.
///
/// <b>Implementations must never throw.</b> A missing file, a permission denial or a torn read all
/// degrade to the empty contract (<c>null</c> / empty list), because these files vanish and change shape
/// under the reader constantly.
///
/// <b>Implementations must be stateless.</b> <c>HardwareProviders</c> members are constructed once per
/// consuming page and run concurrently in <c>DeviceInventory.LoadAsync</c>'s <c>Task.WhenAll</c>; a pure
/// wrapper satisfies that trivially.
///
/// <b>Callers build paths with string concatenation and forward-slash literals — never
/// <c>Path.Combine</c>.</b> On a Windows dev box <c>Path.Combine("/proc", "stat")</c> yields
/// <c>/proc\stat</c>, and every fixture lookup then silently misses.
/// </summary>
internal interface IProcFileSystem {
    /// <summary>Whether the path names an existing file or directory.</summary>
    bool Exists(string path);

    /// <summary>The whole file, or <c>null</c> on any failure.</summary>
    string? ReadAllText(string path);

    /// <summary>The file's lines, or an empty list on any failure.</summary>
    IReadOnlyList<string> ReadAllLines(string path);

    /// <summary>The directory's entry names (not full paths), or an empty list on any failure.</summary>
    IReadOnlyList<string> ListDirectory(string path);

    /// <summary>The final target of a symlink (<c>/sys/block/*/device</c>, <c>/proc/*/fd/*</c>) as a full
    /// path, or <c>null</c> when the path is not a link or cannot be resolved.</summary>
    string? ResolveLink(string path);
}
