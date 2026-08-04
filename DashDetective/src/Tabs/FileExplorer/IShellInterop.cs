using System;

namespace DashDetective.Tabs.FileExplorer;

/// <summary>
/// The desktop-shell operations File Explorer needs. Implementations must never throw: a missing file,
/// a denied handle or a busy shell yields a neutral value or does nothing.
/// </summary>
internal interface IShellInterop {
    /// <summary>The shell's friendly type description for an entry (e.g. "PDF Document", "File folder").
    /// Always returns something — implementations fall back to an extension-derived label.</summary>
    string GetTypeName(string path, bool isDirectory);

    /// <summary>Opens a file with its default application, or a folder in the desktop file manager.</summary>
    void Open(string path);

    /// <summary>Shows the native file/folder Properties dialog. Needs the owning window handle, so it is
    /// reached from the view code-behind through the view model.</summary>
    void ShowProperties(IntPtr owner, string path);

    /// <summary>The shell for this machine, or one that still opens files (that part is portable) but has
    /// no type names or Properties dialog to offer.</summary>
    static IShellInterop ForCurrentPlatform() =>
        OperatingSystem.IsWindows() ? new WindowsShellInterop() : new UnsupportedShellInterop();
}
