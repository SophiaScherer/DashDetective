using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace DashDetective.Tabs.Processes;

/// <summary>
/// Feature-local Win32 interop for the process table: per-process I/O counters (kernel32) and the native
/// Properties sheet for a process's executable (shell32). Mirrors File Explorer's
/// <c>WindowsShellInterop</c> — duplicated tab-local rather than shared, per the self-contained-tab rule.
/// Classic <see cref="DllImportAttribute"/> with <see cref="CharSet.Unicode"/> and soft-fail; the platform
/// check lives in <see cref="IProcessInterop.ForCurrentPlatform"/>.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsProcessInterop : IProcessInterop {
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

    public bool TryGetIoBytes(Process process, out ulong totalBytes) {
        totalBytes = 0;
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
    /// The exe path is resolved from the PID on demand (deferred from the snapshot to keep polling
    /// cheap); a protected/elevated process that denies <c>MainModule</c>, or one that has exited, simply
    /// shows nothing.
    /// </summary>
    public void ShowProperties(IntPtr owner, int pid) {
        string? path = null;
        try {
            using var process = Process.GetProcessById(pid);
            path = process.MainModule?.FileName;
        } catch (Exception e) when (e is ArgumentException or Win32Exception or InvalidOperationException) {
            // The process exited between the snapshot and this call (ArgumentException, or
            // InvalidOperationException once the handle is stale), or it is protected and denies
            // MainModule (Win32Exception). Nothing broader is caught: the codebase's catch-filter idiom
            // (see NativeLoadFailure.Matches) exists so a genuine bug here is not read as "no path".
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

/// <summary>The no-interop set: no I/O figures (the Disk column stays blank) and no Properties dialog —
/// what the old <c>OperatingSystem.IsWindows()</c> guards returned.</summary>
internal sealed class UnsupportedProcessInterop : IProcessInterop {
    public bool TryGetIoBytes(Process process, out ulong totalBytes) {
        totalBytes = 0;
        return false;
    }

    public void ShowProperties(IntPtr owner, int pid) { }
}
