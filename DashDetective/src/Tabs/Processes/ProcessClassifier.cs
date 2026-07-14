using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace DashDetective.Tabs.Processes;

/// <summary>
/// Works out, once per snapshot, the two things managed <see cref="System.Diagnostics.Process"/>
/// enumeration can't tell us but Task Manager relies on: each process's <b>parent PID</b> (so
/// <see cref="ProcessTreeBuilder"/> can collapse a multi-process app under one entry) and its
/// <b>category</b> (App / Background / Windows).
///
/// Classification mirrors Task Manager as closely as documented Win32 allows:
/// <list type="bullet">
/// <item><b>App</b> — the process owns a visible, non-cloaked, un-owned, non-tool top-level window on
/// the interactive desktop (the classic "alt-tab window" test via <c>EnumWindows</c>), excluding the
/// shell window itself. UWP apps whose frame window belongs to <c>ApplicationFrameHost.exe</c> are
/// re-attributed to the actual hosted process, the way Task Manager shows the real app.</item>
/// <item><b>Windows</b> — a process running outside the interactive session (Session 0 isolation puts
/// services and OS components there: svchost, services.exe, csrss, lsass, the System process, …), or
/// one whose session can't be read (protected/system).</item>
/// <item><b>Background</b> — everything else in the interactive session with no app window.</item>
/// </list>
///
/// Task Manager uses undocumented internal heuristics, so this is "close and correct," not byte-exact
/// on every edge case. Static + soft-failing, matching the tab's other providers; on non-Windows it
/// yields an empty classification (everything falls back to Background).
/// </summary>
public static class ProcessClassifier {
    // ----- Toolhelp: parent PID + image name for every live process, in one snapshot -----
    private const uint Th32csSnapProcess = 0x00000002;
    private const int MaxPath = 260;
    private static readonly IntPtr InvalidHandle = new(-1);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ProcessEntry32 {
        public uint dwSize;
        public uint cntUsage;
        public uint th32ProcessID;
        public IntPtr th32DefaultHeapID;
        public uint th32ModuleID;
        public uint cntThreads;
        public uint th32ParentProcessID;
        public int pcPriClassBase;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MaxPath)]
        public string szExeFile;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint processId);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool Process32First(IntPtr snapshot, ref ProcessEntry32 entry);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool Process32Next(IntPtr snapshot, ref ProcessEntry32 entry);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ProcessIdToSessionId(uint processId, out uint sessionId);

    // ----- Window enumeration: which processes own a real app window -----
    private const uint GwOwner = 4;
    private const int GwlExStyle = -20;
    private const long WsExToolWindow = 0x00000080;
    private const int DwmwaCloaked = 14;

    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);
    [DllImport("user32.dll")]
    private static extern bool EnumChildWindows(IntPtr parent, EnumWindowsProc callback, IntPtr lParam);
    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr hwnd, uint command);
    [DllImport("user32.dll")]
    private static extern IntPtr GetShellWindow();
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
    // GetWindowLongPtrW: the 64-bit entry point (this app ships x64). On Win32 it maps to GetWindowLongW.
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);
    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out int value, int size);

    /// <summary>
    /// Captures the classification context for the current instant: parent PIDs and each process's
    /// category. Never throws — a failure of any native step just leaves those entries absent (they
    /// then fall back to Background / parent 0).
    /// </summary>
    public static ProcessClassification Capture() {
        var parent = new Dictionary<int, int>();
        var image = new Dictionary<int, string>();
        var category = new Dictionary<int, ProcessCategory>();

        if (!OperatingSystem.IsWindows())
            return new ProcessClassification(parent, category);

        CaptureProcessTree(parent, image);

        var interactiveSession = CurrentSession();
        var appPids = CaptureAppPids(image);

        foreach (var pid in parent.Keys) {
            if (appPids.Contains(pid))
                category[pid] = ProcessCategory.App;
            else
                category[pid] = IsWindowsProcess(pid, interactiveSession)
                    ? ProcessCategory.Windows
                    : ProcessCategory.Background;
        }

        return new ProcessClassification(parent, category);
    }

    /// <summary>Walks the toolhelp snapshot, recording PID → parent PID and PID → image name.</summary>
    private static void CaptureProcessTree(Dictionary<int, int> parent, Dictionary<int, string> image) {
        var snapshot = CreateToolhelp32Snapshot(Th32csSnapProcess, 0);
        if (snapshot == InvalidHandle)
            return;
        try {
            var entry = new ProcessEntry32 { dwSize = (uint)Marshal.SizeOf<ProcessEntry32>() };
            if (!Process32First(snapshot, ref entry))
                return;
            do {
                var pid = (int)entry.th32ProcessID;
                parent[pid] = (int)entry.th32ParentProcessID;
                image[pid] = entry.szExeFile ?? "";
            } while (Process32Next(snapshot, ref entry));
        } catch {
            // Handle race on a vanishing process — take whatever we gathered.
        } finally {
            CloseHandle(snapshot);
        }
    }

    /// <summary>The PIDs that own a real, visible top-level app window (UWP frames re-attributed to the
    /// hosted process). This is the "App" set.</summary>
    private static HashSet<int> CaptureAppPids(Dictionary<int, string> image) {
        var pids = new HashSet<int>();
        var shell = GetShellWindow();

        EnumWindows((hwnd, _) => {
            if (!IsAppWindow(hwnd, shell))
                return true;
            if (GetWindowThreadProcessId(hwnd, out var raw) == 0 || raw == 0)
                return true;

            var pid = (int)raw;
            // UWP/packaged apps show a frame window owned by ApplicationFrameHost.exe; the real app is a
            // child window in a different process. Re-attribute to it so the app shows under its own name.
            if (image.TryGetValue(pid, out var name) &&
                string.Equals(name, "ApplicationFrameHost.exe", StringComparison.OrdinalIgnoreCase)) {
                var hosted = HostedPid(hwnd, pid);
                if (hosted != 0)
                    pid = hosted;
            }

            pids.Add(pid);
            return true;
        }, IntPtr.Zero);

        return pids;
    }

    /// <summary>The classic "alt-tab window" test: visible, un-owned, not a tool window, not DWM-cloaked,
    /// and not the shell/desktop window.</summary>
    private static bool IsAppWindow(IntPtr hwnd, IntPtr shell) {
        if (hwnd == shell || hwnd == IntPtr.Zero)
            return false;
        if (!IsWindowVisible(hwnd))
            return false;
        if (GetWindow(hwnd, GwOwner) != IntPtr.Zero)
            return false;
        if (((long)GetWindowLongPtr(hwnd, GwlExStyle) & WsExToolWindow) != 0)
            return false;
        if (DwmGetWindowAttribute(hwnd, DwmwaCloaked, out var cloaked, sizeof(int)) == 0 && cloaked != 0)
            return false;
        return true;
    }

    /// <summary>For an ApplicationFrameHost frame window, the PID of the first child window belonging to a
    /// different process — the actual hosted UWP app. 0 if none is found.</summary>
    private static int HostedPid(IntPtr frame, int framePid) {
        var hosted = 0;
        EnumChildWindows(frame, (child, _) => {
            if (GetWindowThreadProcessId(child, out var cpid) != 0 && cpid != 0 && (int)cpid != framePid) {
                hosted = (int)cpid;
                return false; // stop at the first hosted process
            }
            return true;
        }, IntPtr.Zero);
        return hosted;
    }

    private static int CurrentSession() =>
        ProcessIdToSessionId((uint)Environment.ProcessId, out var session) ? (int)session : 1;

    /// <summary>A process is a "Windows process" when it lives outside the interactive session (Session 0
    /// isolation) or its session can't be read (protected/system).</summary>
    private static bool IsWindowsProcess(int pid, int interactiveSession) =>
        ProcessIdToSessionId((uint)pid, out var session) ? (int)session != interactiveSession : true;
}

/// <summary>
/// The immutable result of <see cref="ProcessClassifier.Capture"/> for one snapshot: parent PIDs and
/// categories keyed by PID. Lookups soft-fall-back (parent 0, category Background) for any PID missing
/// from the snapshot (e.g. a process that appeared in the managed enumeration a moment later).
/// </summary>
public sealed class ProcessClassification {
    private readonly IReadOnlyDictionary<int, int> _parentByPid;
    private readonly IReadOnlyDictionary<int, ProcessCategory> _categoryByPid;

    public ProcessClassification(
        IReadOnlyDictionary<int, int> parentByPid,
        IReadOnlyDictionary<int, ProcessCategory> categoryByPid) {
        _parentByPid = parentByPid;
        _categoryByPid = categoryByPid;
    }

    public int ParentOf(int pid) => _parentByPid.TryGetValue(pid, out var parent) ? parent : 0;

    public ProcessCategory CategoryOf(int pid) =>
        _categoryByPid.TryGetValue(pid, out var category) ? category : ProcessCategory.Background;
}
