using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;

namespace DashDetective.Tabs.Processes;

/// <summary>
/// Per-process GPU utilisation via the Windows PDH <c>\GPU Engine(*)\Utilization Percentage</c>
/// counter — the same source the Dashboard's <c>GpuUsageSampler</c> reads for the total, but keyed per
/// process. Instance names look like <c>pid_1234_luid_0x0_0xABCD_phys_0_eng_0_engtype_3D</c>;
/// utilisation is grouped by (pid, engine type), summed within an engine type, and each process's GPU%
/// is its busiest engine type — mirroring the Dashboard's total aggregation, scoped per PID, and
/// matching Task Manager's per-process GPU figure.
///
/// Static like <see cref="ProcessSnapshotProvider"/> (its sole caller, which polls from one timer with
/// an in-flight guard). The PDH query is opened lazily and lives for the app's lifetime — the OS
/// reclaims it at exit — so there is no disposal to thread through the app-lifetime-singleton tab. Any
/// failure yields an empty map, so the GPU column simply shows 0.
/// </summary>
public static class ProcessGpuSampler {
    private const uint ErrorSuccess = 0x00000000;
    private const uint PdhMoreData = 0x800007D2;
    private const uint PdhFmtDouble = 0x00000200;
    private const string CounterPath = @"\GPU Engine(*)\Utilization Percentage";

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
            return Aggregate(buffer, itemCount);
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

    /// <summary>Groups instances by (pid, engine type), sums within an engine type, and takes each
    /// PID's busiest engine type as its GPU% (clamped 0–100).</summary>
    private static Dictionary<int, double> Aggregate(IntPtr buffer, uint itemCount) {
        var itemSize = Marshal.SizeOf<CounterValueItem>();
        var perPidEngine = new Dictionary<(int Pid, string Engine), double>();

        for (var i = 0; i < itemCount; i++) {
            var item = Marshal.PtrToStructure<CounterValueItem>(buffer + i * itemSize);
            if (item.Name == IntPtr.Zero)
                continue;

            var name = Marshal.PtrToStringUni(item.Name);
            if (!TryParse(name, out var pid, out var engine))
                continue;

            var key = (pid, engine);
            perPidEngine.TryGetValue(key, out var running);
            perPidEngine[key] = running + item.Value;
        }

        var result = new Dictionary<int, double>();
        foreach (var (key, value) in perPidEngine) {
            var clamped = value < 0 ? 0 : value > 100 ? 100 : value;
            if (!result.TryGetValue(key.Pid, out var current) || clamped > current)
                result[key.Pid] = clamped;
        }
        return result;
    }

    /// <summary>Pulls the PID (digits after <c>pid_</c>) and engine type (after the trailing
    /// <c>engtype_</c>) from a GPU-engine instance name.</summary>
    private static bool TryParse(string? instanceName, out int pid, out string engine) {
        pid = 0;
        engine = "";
        if (string.IsNullOrEmpty(instanceName))
            return false;

        const string pidToken = "pid_";
        var pidIdx = instanceName.IndexOf(pidToken, StringComparison.Ordinal);
        if (pidIdx < 0)
            return false;

        var start = pidIdx + pidToken.Length;
        var end = start;
        while (end < instanceName.Length && char.IsDigit(instanceName[end]))
            end++;
        if (end == start ||
            !int.TryParse(instanceName.AsSpan(start, end - start), NumberStyles.Integer, CultureInfo.InvariantCulture, out pid))
            return false;

        const string engToken = "engtype_";
        var engIdx = instanceName.LastIndexOf(engToken, StringComparison.Ordinal);
        engine = engIdx < 0 ? "" : instanceName[(engIdx + engToken.Length)..];
        return true;
    }
}
