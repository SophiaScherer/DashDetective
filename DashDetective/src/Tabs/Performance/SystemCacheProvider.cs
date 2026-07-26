using DashDetective.Services.Diagnostics;
using System;
using System.Runtime.InteropServices;

namespace DashDetective.Tabs.Performance;

/// <summary>
/// Reads the system file-cache size — Task Manager's memory "Cached" figure — via the in-box psapi export
/// <c>GetPerformanceInfo</c>. Its <c>PERFORMANCE_INFORMATION</c> struct reports <c>SystemCache</c> in pages
/// alongside the <c>PageSize</c> to scale them by, so no PDH counter and no admin rights are involved. Like
/// <c>GlobalMemoryStatusEx</c> this is an absolute one-shot reading, so there is no prior state to seed or
/// diff. Every failure — non-Windows, a native <c>FALSE</c>, an implausible reading — soft-fails to
/// <c>null</c> rather than throwing; the first thrown exception is logged and then latches the provider off,
/// so a persistent fault cannot flood the log at the sampling cadence.
///
/// Page-local to the Performance tab, following the feature-local P/Invoke precedent of File Explorer's
/// <c>ShellInterop</c> and the Network tab's <c>ConnectionsInterop</c>. Deliberately separate from the shared
/// <c>MemoryUsageSampler</c>, which Dashboard and Processes also consume.
/// </summary>
internal static class SystemCacheProvider {
    // Latched by the first thrown exception: the export is either present for the process's lifetime or not,
    // so there is nothing to gain from re-invoking it every tick after a hard failure.
    private static bool _unavailable;

    /// <summary>System cache size in bytes, or <c>null</c> when unavailable (non-Windows, a native failure,
    /// or a nonsensical page count/size).</summary>
    public static ulong? ReadCachedBytes() {
        if (!OperatingSystem.IsWindows() || _unavailable)
            return null;

        try {
            // Cb must be set before the call so the OS knows the struct version/size.
            var info = new PerformanceInformation { Cb = (uint)Marshal.SizeOf<PerformanceInformation>() };
            if (!GetPerformanceInfo(ref info, info.Cb))
                return null;

            return ToBytes(info.SystemCache, info.PageSize);
        } catch (Exception e) {
            Log.Warn("SystemCacheProvider read failed", e);
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
