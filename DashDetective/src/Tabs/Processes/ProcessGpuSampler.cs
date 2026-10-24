using DashDetective.Services.SystemMetrics;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace DashDetective.Tabs.Processes;

/// <summary>
/// Per-process GPU utilization via the Windows PDH <c>\GPU Engine(*)\Utilization Percentage</c>
/// counter — the same source the Dashboard's <c>GpuUsageSampler</c> reads for the total, but keyed per
/// process. A process's GPU% is its busiest physical engine across every adapter, Task Manager's
/// per-process figure; the rule is <see cref="GpuEngineLoad.ByProcess"/>, shared with the adapter totals.
///
/// Static like <see cref="ProcessSnapshotProvider"/> (its sole caller, which polls from one timer with
/// an in-flight guard). The PDH query is opened lazily and lives for the app's lifetime — the OS
/// reclaims it at exit — so there is no disposal to thread through the app-lifetime-singleton tab. Any
/// failure yields an empty map, so the GPU column simply shows 0. The platform check lives in
/// <see cref="IProcessSnapshotProvider.ForCurrentPlatform"/>.
///
/// <see cref="Sample"/> carries the platform attribute rather than the type, so the pinned
/// <see cref="CounterPath"/> stays readable from tests on every CI leg.
/// </summary>
public static class ProcessGpuSampler {
    private const uint ErrorSuccess = 0x00000000;
    private const uint PdhMoreData = 0x800007D2;
    private const uint PdhFmtDouble = 0x00000200;
    internal const string CounterPath = @"\GPU Engine(*)\Utilization Percentage";

    [StructLayout(LayoutKind.Sequential)]
    private struct CounterValueItem {
        public IntPtr Name;
        public uint CStatus;
        // 4 bytes of padding land here so Value is 8-byte aligned (as in GpuUsageSampler).
        public double Value;
    }

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern uint PdhOpenQuery(string? dataSource, IntPtr userData, out IntPtr query);
    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern uint PdhAddEnglishCounter(IntPtr query, string counterPath, IntPtr userData, out IntPtr counter);
    [DllImport("pdh.dll")]
    private static extern uint PdhCollectQueryData(IntPtr query);
    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern uint PdhGetFormattedCounterArray(IntPtr counter, uint format, ref uint bufferSize, out uint itemCount, IntPtr buffer);

    private static readonly Dictionary<int, double> EmptyMap = new();

    private static IntPtr _query;
    private static IntPtr _counter;
    private static bool _initialized;
    private static bool _ready;

    /// <summary>
    /// Returns a PID → GPU% map for the current interval. The first call primes the rate counter and
    /// returns empty; any failure also returns an empty map (the GPU column then reads 0).
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static IReadOnlyDictionary<int, double> Sample() {
        if (!EnsureReady() || PdhCollectQueryData(_query) != ErrorSuccess)
            return EmptyMap;

        // First call sizes the buffer (returns PDH_MORE_DATA); the second fills it.
        uint bufferSize = 0;
        var status = PdhGetFormattedCounterArray(_counter, PdhFmtDouble, ref bufferSize, out _, IntPtr.Zero);
        if (status != PdhMoreData || bufferSize == 0)
            return EmptyMap;

        var buffer = Marshal.AllocHGlobal((int)bufferSize);
        try {
            if (PdhGetFormattedCounterArray(_counter, PdhFmtDouble, ref bufferSize, out var itemCount, buffer) != ErrorSuccess)
                return EmptyMap;
            return GpuEngineLoad.ByProcess(ReadItems(buffer, itemCount));
        } finally {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static bool EnsureReady() {
        if (_initialized)
            return _ready;
        _initialized = true;

        if (!OperatingSystem.IsWindows())
            return false;
        if (PdhOpenQuery(null, IntPtr.Zero, out _query) != ErrorSuccess)
            return false;
        if (PdhAddEnglishCounter(_query, CounterPath, IntPtr.Zero, out _counter) != ErrorSuccess)
            return false;

        // Prime once so the first real Sample() reflects an interval (a rate counter needs two points).
        PdhCollectQueryData(_query);
        _ready = true;
        return true;
    }

    /// <summary>Marshals a formatted counter array into (instance name, value) pairs.</summary>
    private static List<(string? Name, double Value)> ReadItems(IntPtr buffer, uint itemCount) {
        var itemSize = Marshal.SizeOf<CounterValueItem>();
        var items = new List<(string?, double)>((int)itemCount);
        for (var i = 0; i < itemCount; i++) {
            var item = Marshal.PtrToStructure<CounterValueItem>(buffer + i * itemSize);
            if (item.Name != IntPtr.Zero)
                items.Add((Marshal.PtrToStringUni(item.Name), item.Value));
        }
        return items;
    }
}
