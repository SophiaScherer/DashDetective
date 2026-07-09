using System;
using System.IO;
using System.Runtime.InteropServices;

namespace DashDetective.Tabs.FileExplorer;

/// <summary>
/// Feature-local Win32 shell interop. Follows the Dashboard sampler conventions: classic
/// <see cref="DllImportAttribute"/> with <see cref="CharSet.Unicode"/>, a private nested
/// sequential struct, and soft-fail (a native failure yields a neutral value, never an exception).
/// Currently exposes the shell's friendly type name via <c>SHGetFileInfo</c> — icons are drawn as
/// themed vector glyphs, so no HICON is requested. <c>SHObjectProperties</c> is added in Phase 5.
/// </summary>
public static class ShellInterop {
    /// <summary>
    /// Returns the shell's friendly type description for an entry (e.g. "PDF Document",
    /// "File folder"). Uses <c>SHGFI_USEFILEATTRIBUTES</c> so the name is derived from the path +
    /// attributes without touching the file (fast, and safe on locked entries). Falls back to an
    /// extension-based label, then empty.
    /// </summary>
    public static string GetTypeName(string path, bool isDirectory) {
        if (!OperatingSystem.IsWindows())
            return isDirectory ? "File folder" : ExtensionLabel(path);

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

        return isDirectory ? "File folder" : ExtensionLabel(path);
    }

    // "config.json" -> "JSON File"; no extension -> "File".
    private static string ExtensionLabel(string path) {
        var ext = Path.GetExtension(path);
        return string.IsNullOrEmpty(ext) ? "File" : $"{ext.TrimStart('.').ToUpperInvariant()} File";
    }

    private const uint SHGFI_TYPENAME = 0x000000400;
    private const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;
    private const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;
    private const uint FILE_ATTRIBUTE_DIRECTORY = 0x00000010;

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
}
