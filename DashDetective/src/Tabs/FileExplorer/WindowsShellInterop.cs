using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace DashDetective.Tabs.FileExplorer;

/// <summary>
/// Feature-local Win32 shell interop: classic <see cref="DllImportAttribute"/> with
/// <see cref="CharSet.Unicode"/>, a private nested sequential struct, and soft-fail (a native failure
/// yields a neutral value, never an exception). Exposes the shell's friendly type name via
/// <c>SHGetFileInfo</c> — icons are drawn as themed vector glyphs, so no HICON is requested. The platform
/// check lives in <see cref="IShellInterop.ForCurrentPlatform"/>.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsShellInterop : IShellInterop {
    /// <summary>
    /// Returns the shell's friendly type description for an entry (e.g. "PDF Document",
    /// "File folder"). Uses <c>SHGFI_USEFILEATTRIBUTES</c> so the name is derived from the path +
    /// attributes without touching the file (fast, and safe on locked entries). Falls back to an
    /// extension-based label.
    /// </summary>
    public string GetTypeName(string path, bool isDirectory) {
        try {
            var info = new SHFILEINFO();
            uint attrs = isDirectory ? FILE_ATTRIBUTE_DIRECTORY : FILE_ATTRIBUTE_NORMAL;
            var result = SHGetFileInfo(path, attrs, ref info, (uint)Marshal.SizeOf<SHFILEINFO>(),
                                       SHGFI_TYPENAME | SHGFI_USEFILEATTRIBUTES);
            if (result != IntPtr.Zero && !string.IsNullOrWhiteSpace(info.szTypeName))
                return info.szTypeName;
        } catch {
            // Fall through to the managed fallback.
        }

        return ShellFallback.TypeName(path, isDirectory);
    }

    /// <summary>
    /// Opens a file with its default application, or a folder in Explorer, via the shell
    /// (<c>UseShellExecute</c>). Soft-fails on a missing file / no association / access denied.
    /// </summary>
    public void Open(string path) => ShellFallback.Open(path);

    /// <summary>
    /// Shows the native Windows file/folder Properties dialog. Needs the owning window handle, so it's
    /// reached from the view code-behind through the view model.
    /// </summary>
    public void ShowProperties(IntPtr owner, string path) {
        try {
            SHObjectProperties(owner, SHOP_FILEPATH, path, null);
        } catch {
            // Dialog couldn't be shown (item gone, shell busy) — ignore.
        }
    }

    private const uint SHGFI_TYPENAME = 0x000000400;
    private const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;
    private const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;
    private const uint FILE_ATTRIBUTE_DIRECTORY = 0x00000010;
    private const uint SHOP_FILEPATH = 0x00000002;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFO {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes,
                                               ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SHObjectProperties(IntPtr hwnd, uint shopObjectType,
                                                  string pszObjectName, string? pszPropertyPage);
}

/// <summary>
/// The shell for a host with no Win32 shell: type names fall back to the extension label and Properties
/// does nothing — exactly what the old <c>OperatingSystem.IsWindows()</c> guards produced.
///
/// <b><see cref="Open"/> still opens.</b> It was never guarded: it is managed <c>Process.Start</c> with
/// <c>UseShellExecute</c>, which works on any desktop. No-oping it here would break the very platform
/// this seam is preparing for.
/// </summary>
internal sealed class UnsupportedShellInterop : IShellInterop {
    public string GetTypeName(string path, bool isDirectory) => ShellFallback.TypeName(path, isDirectory);

    public void Open(string path) => ShellFallback.Open(path);

    public void ShowProperties(IntPtr owner, string path) { }
}

/// <summary>The portable parts both implementations share.</summary>
internal static class ShellFallback {
    /// <summary>"config.json" → "JSON File"; no extension → "File".</summary>
    internal static string TypeName(string path, bool isDirectory) {
        if (isDirectory)
            return "File folder";
        var ext = Path.GetExtension(path);
        return string.IsNullOrEmpty(ext) ? "File" : $"{ext.TrimStart('.').ToUpperInvariant()} File";
    }

    internal static void Open(string path) {
        try {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        } catch {
            // Nothing actionable — the item simply doesn't open.
        }
    }
}
