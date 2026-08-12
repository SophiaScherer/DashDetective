using System;
using System.IO;

namespace DashDetective.Tabs.FileExplorer;

/// <summary>
/// The desktop shell on Linux. There is no native interop here at all: opening is already portable
/// (<c>UseShellExecute</c> hands the path to <c>xdg-open</c>), and type names come from a static table
/// rather than a <c>xdg-mime</c> subprocess per row.
///
/// <b>There is no Properties dialog to show.</b> No desktop environment exposes one to a foreign
/// process, so rather than leave the button dead this opens the containing folder in the desktop's file
/// manager — where the user's own Properties dialog is one right-click away. The
/// <paramref name="owner"/> handle every other implementation needs is unused for the same reason.
///
/// Carries no <c>[SupportedOSPlatform]</c>: it is portable managed code, and the platform is decided in
/// <see cref="IShellInterop.ForCurrentPlatform"/>.
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
    /// reveal. Split out from the launch so the decision is unit-tested without starting a file manager
    /// on the machine running the suite.
    ///
    /// A path with no parent is the filesystem root, which reveals itself — the closest thing to
    /// "show me where this is".
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
