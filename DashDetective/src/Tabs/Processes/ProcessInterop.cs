using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DashDetective.Tabs.Processes;

/// <summary>
/// Feature-local Win32 shell interop for the process table: shows the native Windows Properties sheet
/// for a process's executable. Mirrors File Explorer's <c>ShellInterop</c> (same <c>SHObjectProperties</c>
/// call) — duplicated tab-local rather than shared, per the self-contained-tab rule (promote a shared
/// shell-interop helper if a third caller appears). Classic <see cref="DllImportAttribute"/> with
/// <see cref="CharSet.Unicode"/> and soft-fail, matching the app's interop conventions.
/// </summary>
public static class ProcessInterop {
    private const uint SHOP_FILEPATH = 0x00000002;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SHObjectProperties(IntPtr hwnd, uint shopObjectType,
                                                  string pszObjectName, string? pszPropertyPage);

    /// <summary>
    /// Shows the Windows Properties dialog for the given process's executable. The exe path is resolved
    /// from the PID on demand (deferred from the snapshot to keep polling cheap); a protected/elevated
    /// process that denies <c>MainModule</c>, or one that has exited, simply shows nothing. Needs the
    /// owning window handle, so it's invoked from the view code-behind — the pattern used by the Export
    /// and File Explorer Properties dialogs.
    /// </summary>
    public static void ShowProperties(IntPtr owner, int pid) {
        if (!OperatingSystem.IsWindows())
            return;

        string? path = null;
        try {
            using var process = Process.GetProcessById(pid);
            path = process.MainModule?.FileName;
        } catch {
            // Exited (ArgumentException) or access denied on a protected process (Win32Exception).
        }
        if (string.IsNullOrEmpty(path))
            return;

        try {
            SHObjectProperties(owner, SHOP_FILEPATH, path, null);
        } catch {
            // Dialog couldn't be shown (item gone, shell busy) — ignore.
        }
    }
}
