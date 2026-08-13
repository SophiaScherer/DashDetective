using System;
using System.IO;

namespace DashDetective.Tabs.FileExplorer;

/// <summary>
/// The desktop shell on Linux. No native interop at all — opening is already portable
/// (<c>UseShellExecute</c> reaches <c>xdg-open</c>) and type names come from a static table.
///
/// <b>There is no Properties dialog to show</b>; no desktop exposes one to a foreign process. Rather
/// than leave the button dead it opens the containing folder, where the desktop's own Properties is one
/// right-click away. Portable managed code, so no <c>[SupportedOSPlatform]</c>.
/// </summary>
internal sealed class LinuxShellInterop : IShellInterop {
    public string GetTypeName(string path, bool isDirectory) =>
        isDirectory ? "Folder" : FileTypeDescriptions.For(path);

    public void Open(string path) => ShellFallback.Open(path);

    /// <summary>Opens the entry's containing folder in the desktop's file manager.</summary>
    public void ShowProperties(IntPtr owner, string path) {
        if (RevealTarget(path) is { } target)
            ShellFallback.Open(target);
    }

    /// <summary>
    /// Which folder to open to reveal <paramref name="path"/>, or <c>null</c> when there is nothing to
    /// reveal. Split from the launch so the decision is unit-tested without starting a file manager. A
    /// path with no parent is the filesystem root, which reveals itself.
    /// </summary>
    internal static string? RevealTarget(string path) {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        try {
            var parent = Path.GetDirectoryName(path);
            return string.IsNullOrEmpty(parent) ? path : parent;
        } catch (ArgumentException) {
            return null; // not a usable path
        }
    }
}
