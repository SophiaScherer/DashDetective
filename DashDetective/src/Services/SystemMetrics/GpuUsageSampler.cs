using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace DashDetective.Services.SystemMetrics;

/// <summary>
/// Samples total GPU utilisation via the Windows PDH <c>\GPU Engine(*)\Utilization Percentage</c>
/// performance counter — the same source Task Manager uses. Each <see cref="Sample"/> call returns
/// the current GPU load as a percentage (0–100). No extra dependencies beyond the OS
/// <c>pdh.dll</c>; comparable per-sample cost to the CPU/Memory samplers.
///
/// Shared: the Dashboard and the Performance tab each own an instance. Moved here from
/// src/Tabs/Dashboard with sign-off when the Performance tab was activated — the same precedent as
/// <c>CpuUsageSampler</c> / <c>NetworkUsageSampler</c>.
/// </summary>
public sealed class GpuUsageSampler : IDisposable {
    // PDH status codes and formatting flags (winperf.h / pdhmsg.h).
    private const uint ErrorSuccess = 0x00000000;
    private const uint PdhMoreData = 0x800007D2;
    private const uint PdhFmtDouble = 0x00000200;

    /// <summary>
    /// One formatted counter instance: its instance name plus the value as a double. The layout
    /// mirrors PDH's <c>PDH_FMT_COUNTERVALUE_ITEM</c> — a name pointer followed by
    /// <c>PDH_FMT_COUNTERVALUE</c> (a status word, then the 8-byte-aligned value union).
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct CounterValueItem {
        public IntPtr Name;   // LPWSTR — pointer to the instance name
        public uint CStatus;
        // 4 bytes of padding are inserted here by the runtime so Value lands on an 8-byte boundary.
        public double Value;
    }

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern uint PdhOpenQuery(string? dataSource, IntPtr userData, out IntPtr query);

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern uint PdhAddEnglishCounter(IntPtr query, string counterPath, IntPtr userData, out IntPtr counter);

    [DllImport("pdh.dll")]
    private static extern uint PdhCollectQueryData(IntPtr query);

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern uint PdhGetFormattedCounterArray(
        IntPtr counter, uint format, ref uint bufferSize, out uint itemCount, IntPtr buffer);

    [DllImport("pdh.dll")]
    private static extern uint PdhCloseQuery(IntPtr query);

    private const string CounterPath = @"\GPU Engine(*)\Utilization Percentage";

    private readonly IntPtr _query;
    private readonly IntPtr _counter;
    private readonly bool _ready;

    public GpuUsageSampler() {
        // A failure to stand up the query leaves _ready false; Sample() then returns 0 forever and
        // the caller stops its timer — the same soft-fail contract as the CPU/Memory samplers.
        if (PdhOpenQuery(null, IntPtr.Zero, out _query) != ErrorSuccess)
            return;

        if (PdhAddEnglishCounter(_query, CounterPath, IntPtr.Zero, out _counter) != ErrorSuccess) {
            PdhCloseQuery(_query);
            _query = IntPtr.Zero;
            return;
        }

        // Seed one collect so the first Sample() reflects a real interval. The utilisation counter
        // is a rate that needs two data points, so priming here mirrors CpuUsageSampler seeding
        // GetSystemTimes in its constructor.
        PdhCollectQueryData(_query);
        _ready = true;
    }

    /// <summary>
    /// Returns total GPU utilisation (0–100) at the moment of the call. GPU work is split across
    /// engine types (3D, Copy, VideoDecode, …) with one counter instance per process and engine;
    /// utilisation is summed per engine type and the busiest type is reported, matching Task
    /// Manager's headline figure. Any failure yields 0.
    /// </summary>
    public double Sample() {
        if (!_ready || PdhCollectQueryData(_query) != ErrorSuccess)
            return 0;

        // First call sizes the buffer (returns PDH_MORE_DATA); the second fills it.
        uint bufferSize = 0;
        var status = PdhGetFormattedCounterArray(_counter, PdhFmtDouble, ref bufferSize, out _, IntPtr.Zero);
        if (status != PdhMoreData || bufferSize == 0)
            return 0;

        var buffer = Marshal.AllocHGlobal((int)bufferSize);
        try {
            if (PdhGetFormattedCounterArray(_counter, PdhFmtDouble, ref bufferSize, out var itemCount, buffer) != ErrorSuccess)
                return 0;

            return Aggregate(buffer, itemCount);
        } finally {
            Marshal.FreeHGlobal(buffer);
        }
    }

    /// <summary>
    /// Sums utilisation across the instances of each engine type, then returns the largest
    /// per-engine total (clamped 0–100). Instance names look like
    /// <c>pid_1234_luid_0x0_0xC4C7_phys_0_eng_0_engtype_3D</c>; the <c>luid</c> token identifies
    /// which physical adapter an instance belongs to and is what a future multi-GPU split will key
    /// on, but a single combined reading needs only the <c>engtype</c> grouping.
    /// </summary>
    private static double Aggregate(IntPtr buffer, uint itemCount) {
        var itemSize = Marshal.SizeOf<CounterValueItem>();
        var perEngine = new Dictionary<string, double>(StringComparer.Ordinal);

        for (var i = 0; i < itemCount; i++) {
            var item = Marshal.PtrToStructure<CounterValueItem>(buffer + i * itemSize);
            if (item.Name == IntPtr.Zero)
                continue;

            var name = Marshal.PtrToStringUni(item.Name);
            var engine = EngineType(name);
            if (engine is null)
                continue;

            perEngine.TryGetValue(engine, out var running);
            perEngine[engine] = running + item.Value;
        }

        double max = 0;
        foreach (var total in perEngine.Values)
            if (total > max)
                max = total;

        return max < 0 ? 0 : max > 100 ? 100 : max;
    }

    /// <summary>Extracts the engine type after the trailing <c>engtype_</c> token, or null.</summary>
    private static string? EngineType(string? instanceName) {
        if (string.IsNullOrEmpty(instanceName))
            return null;

        const string token = "engtype_";
        var idx = instanceName.LastIndexOf(token, StringComparison.Ordinal);
        return idx < 0 ? null : instanceName[(idx + token.Length)..];
    }

    /// <summary>Closes the PDH query handle. Safe to call more than once.</summary>
    public void Dispose() {
        if (_query != IntPtr.Zero)
            PdhCloseQuery(_query);
    }
}
