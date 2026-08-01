using DashDetective.Services.Diagnostics;
using System;
using System.Runtime.InteropServices;

namespace DashDetective.Services.SystemMetrics;

/// <summary>
/// One system-wide counters snapshot: the file-cache size in bytes (<c>null</c> when the reading is
/// implausible) plus the live process, thread and handle totals — the figures Task Manager shows on its
/// CPU and Memory panes.
/// </summary>
public readonly record struct SystemPerformanceSample(
    ulong? CachedBytes, int ProcessCount, int ThreadCount, int HandleCount);

/// <summary>
/// Reads the system-wide counters — file-cache size (Task Manager's memory "Cached" figure) and the
/// process / thread / handle totals — via the in-box psapi export <c>GetPerformanceInfo</c>. Its
/// <c>PERFORMANCE_INFORMATION</c> struct reports all four from a single call, so one read serves both the
/// Performance tab's memory and CPU panes: no PDH counter, no admin rights, and no per-tick process
/// enumeration. Like <c>GlobalMemoryStatusEx</c> this is an absolute one-shot reading, so there is no prior
/// state to seed or diff. Every failure — non-Windows, a native <c>FALSE</c> — soft-fails to <c>null</c>
/// rather than throwing; the first thrown exception is logged and then latches the provider off, so a
/// persistent fault cannot flood the log at the sampling cadence.
///
/// Lives here rather than in a tab folder because the Performance and Processes tabs both read these counts
/// and must agree on them. Deliberately separate from <see cref="MemoryUsageSampler"/>, which reports
/// physical memory instead.
/// </summary>
internal static class SystemPerformanceProvider {
    // Latched by the first thrown exception: the export is either present for the process's lifetime or not,
    // so there is nothing to gain from re-invoking it every tick after a hard failure.
    private static bool _unavailable;

    /// <summary>The current system counters, or <c>null</c> when unavailable (non-Windows or a native
    /// failure). A successful read whose cache figure is nonsensical still returns a sample — only its
    /// <see cref="SystemPerformanceSample.CachedBytes"/> is <c>null</c>.</summary>
    public static SystemPerformanceSample? Read() {
        if (!OperatingSystem.IsWindows() || _unavailable)
            return null;

        try {
            // Cb must be set before the call so the OS knows the struct version/size.
            var info = new PerformanceInformation { Cb = (uint)Marshal.SizeOf<PerformanceInformation>() };
            if (!GetPerformanceInfo(ref info, info.Cb))
                return null;

            return new SystemPerformanceSample(
                ToBytes(info.SystemCache, info.PageSize),
                (int)info.ProcessCount, (int)info.ThreadCount, (int)info.HandleCount);
        } catch (Exception e) {
            Log.Warn("SystemPerformanceProvider read failed", e);
            _unavailable = true;
            return null;
        }
    }

    /// <summary>Scales a page count by the system page size, returning <c>null</c> when either input is zero
    /// ("not reported") or when the product would overflow.</summary>
    internal static ulong? ToBytes(ulong pages, ulong pageSize) {
        if (pages == 0 || pageSize == 0)
            return null;

        return pages <= ulong.MaxValue / pageSize ? pages * pageSize : null;
    }

    // SIZE_T maps to nuint; the default sequential layout supplies the padding after Cb on 64-bit.
    [StructLayout(LayoutKind.Sequential)]
    private struct PerformanceInformation {
        public uint Cb;
        public nuint CommitTotal;
        public nuint CommitLimit;
        public nuint CommitPeak;
        public nuint PhysicalTotal;
        public nuint PhysicalAvailable;
        public nuint SystemCache;
        public nuint KernelTotal;
        public nuint KernelPaged;
        public nuint KernelNonpaged;
        public nuint PageSize;
        public uint HandleCount;
        public uint ProcessCount;
        public uint ThreadCount;
    }

    [DllImport("psapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetPerformanceInfo(ref PerformanceInformation info, uint cb);
}
