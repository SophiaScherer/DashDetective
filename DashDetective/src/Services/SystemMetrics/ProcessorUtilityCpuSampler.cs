using System;
using System.Runtime.InteropServices;

namespace DashDetective.Services.SystemMetrics;

/// <summary>
/// Samples total CPU utilisation via the Windows PDH <c>\Processor Information(_Total)\% Processor
/// Utility</c> performance counter — the same source Task Manager uses since Windows 8. Unlike the
/// idle-based "% Processor Time" method (<see cref="SystemTimesCpuSampler"/>), Utility normalises for
/// frequency scaling / Turbo Boost, so it tracks Task Manager instead of reading slightly low under
/// boost. Single-instance counter, so it uses <c>PdhGetFormattedCounterValue</c> (simpler than the
/// per-instance array the GPU sampler needs). Only OS <c>pdh.dll</c>; comparable per-sample cost to
/// the other samplers.
/// </summary>
internal sealed class ProcessorUtilityCpuSampler : ICpuSampler, IDisposable {
    // PDH status codes and formatting flags (pdhmsg.h / winperf.h).
    private const uint ErrorSuccess = 0x00000000;
    private const uint PdhFmtDouble = 0x00000200;

    /// <summary>Formatted single-counter value — mirrors PDH's <c>PDH_FMT_COUNTERVALUE</c>: a status
    /// word then the 8-byte-aligned value union.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct FormattedValue {
        public uint CStatus;
        // 4 bytes of padding are inserted here so Value lands on an 8-byte boundary.
        public double Value;
    }

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern uint PdhOpenQuery(string? dataSource, IntPtr userData, out IntPtr query);

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern uint PdhAddEnglishCounter(IntPtr query, string counterPath, IntPtr userData, out IntPtr counter);

    [DllImport("pdh.dll")]
    private static extern uint PdhCollectQueryData(IntPtr query);

    [DllImport("pdh.dll")]
    private static extern uint PdhGetFormattedCounterValue(IntPtr counter, uint format, IntPtr type, out FormattedValue value);

    [DllImport("pdh.dll")]
    private static extern uint PdhCloseQuery(IntPtr query);

    private const string CounterPath = @"\Processor Information(_Total)\% Processor Utility";

    private IntPtr _query;
    private IntPtr _counter;

    /// <summary>Whether the counter stood up; when false the caller uses the fallback sampler.</summary>
    public bool Ready { get; }

    public ProcessorUtilityCpuSampler() {
        // A failure to stand up the query leaves Ready false; CpuUsageSampler then falls back to
        // GetSystemTimes. Mirrors GpuUsageSampler's soft-fail contract.
        if (PdhOpenQuery(null, IntPtr.Zero, out _query) != ErrorSuccess)
            return;

        if (PdhAddEnglishCounter(_query, CounterPath, IntPtr.Zero, out _counter) != ErrorSuccess) {
            PdhCloseQuery(_query);
            _query = IntPtr.Zero;
            return;
        }

        // Seed one collect so the first Sample() reflects a real interval — the utilisation counter
        // is a rate that needs two data points, mirroring SystemTimesCpuSampler seeding GetSystemTimes.
        PdhCollectQueryData(_query);
        Ready = true;
    }

    /// <summary>
    /// Returns total CPU utilisation (0–100) since the previous call. Utility can exceed 100 while the
    /// CPU boosts above its base clock; it is clamped to 100 to match Task Manager's headline figure.
    /// Any failure yields 0.
    /// </summary>
    public double Sample() {
        if (!Ready || PdhCollectQueryData(_query) != ErrorSuccess)
            return 0;

        if (PdhGetFormattedCounterValue(_counter, PdhFmtDouble, IntPtr.Zero, out var value) != ErrorSuccess)
            return 0;

        var usage = value.Value;
        return usage < 0 ? 0 : usage > 100 ? 100 : usage;
    }

    /// <summary>Closes the PDH query handle. Safe to call more than once.</summary>
    public void Dispose() {
        if (_query != IntPtr.Zero) {
            PdhCloseQuery(_query);
            _query = IntPtr.Zero;
        }
    }
}
