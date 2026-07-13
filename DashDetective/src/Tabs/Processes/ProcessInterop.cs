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

    // Cumulative per-process I/O byte counts (kernel32). Read/Write transfer counts are total bytes
    // moved through ReadFile/WriteFile — a superset of physical disk (includes cache and non-disk file
    // I/O), so it's a slightly broad but honest approximation of Task Manager's Disk figure; "Other"
    // (device IOCTLs) is excluded.
    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetProcessIoCounters(IntPtr hProcess, out IoCounters counters);

    /// <summary>
    /// Reads a process's cumulative read+write transfer bytes for the Disk column. The caller diffs
    /// these over the sample interval to get a rate. Soft-fails (returns false) when the process denies
    /// a handle (protected/elevated) or has exited.
    /// </summary>
    public static bool TryGetIoBytes(Process process, out ulong totalBytes) {
        totalBytes = 0;
        if (!OperatingSystem.IsWindows())
            return false;
        try {
            if (GetProcessIoCounters(process.Handle, out var counters)) {
                totalBytes = counters.ReadTransferCount + counters.WriteTransferCount;
                return true;
            }
        } catch {
            // process.Handle denied (protected process without elevation) — no I/O reading.
        }
        return false;
    }

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
