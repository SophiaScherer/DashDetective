using System;
using System.Runtime.InteropServices;

namespace DashDetective.Services.SystemMetrics;

/// <summary>
/// Samples total physical-disk activity via the Windows PDH <c>\PhysicalDisk(_Total)\% Idle Time</c>
/// performance counter and reports the inverse — <c>active = 100 − idle</c>, clamped 0–100 — which is
/// Task Manager's disk "Active time" (the <c>% Disk Time</c> counter can read above 100% under load,
/// so idle time is the reliable source). Each <see cref="Sample"/> call returns the current disk
/// activity as a percentage (0–100). No extra dependencies beyond the OS <c>pdh.dll</c>; comparable
/// per-sample cost to the CPU/GPU samplers.
///
/// Shared: the Dashboard and the Performance tab each own an instance. Moved here from
/// src/Tabs/Dashboard with sign-off when the Performance tab was activated — the same precedent as
/// <c>CpuUsageSampler</c> / <c>NetworkUsageSampler</c>.
/// </summary>
public sealed class StorageUsageSampler : IDisposable {
    // PDH status codes and formatting flags (winperf.h / pdhmsg.h).
    private const uint ErrorSuccess = 0x00000000;
    private const uint PdhFmtDouble = 0x00000200;
    private const uint PdhCstatusValidData = 0x00000000;
    private const uint PdhCstatusNewData = 0x00000001;

    /// <summary>
    /// One formatted counter value. The layout mirrors PDH's <c>PDH_FMT_COUNTERVALUE</c> — a status
    /// word followed by the value union; the runtime inserts 4 bytes of padding after
    /// <see cref="CStatus"/> so <see cref="Value"/> lands on an 8-byte boundary.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct CounterValue {
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

    [DllImport("pdh.dll")]
    private static extern uint PdhGetFormattedCounterValue(IntPtr counter, uint format, out uint counterType, out CounterValue value);

    [DllImport("pdh.dll")]
    private static extern uint PdhCloseQuery(IntPtr query);

    private const string CounterPath = @"\PhysicalDisk(_Total)\% Idle Time";

    private readonly IntPtr _query;
    private readonly IntPtr _counter;
    private readonly bool _ready;

    public StorageUsageSampler() {
        // A failure to stand up the query leaves _ready false; Sample() then returns 0 forever and
        // the caller stops its timer — the same soft-fail contract as the CPU/Memory/GPU samplers.
        if (PdhOpenQuery(null, IntPtr.Zero, out _query) != ErrorSuccess)
            return;

        if (PdhAddEnglishCounter(_query, CounterPath, IntPtr.Zero, out _counter) != ErrorSuccess) {
            PdhCloseQuery(_query);
            _query = IntPtr.Zero;
            return;
        }

        // Seed one collect so the first Sample() reflects a real interval. % Idle Time is a rate
        // that needs two data points, so priming here mirrors GpuUsageSampler seeding its query.
        PdhCollectQueryData(_query);
        _ready = true;
    }

    /// <summary>
    /// Returns total physical-disk activity (0–100) at the moment of the call, as <c>100 − idle</c>
    /// on the aggregate <c>_Total</c> instance. Any failure yields 0.
    /// </summary>
    public double Sample() {
        if (!_ready || PdhCollectQueryData(_query) != ErrorSuccess)
            return 0;

        if (PdhGetFormattedCounterValue(_counter, PdhFmtDouble, out _, out var value) != ErrorSuccess)
            return 0;

        if (value.CStatus != PdhCstatusValidData && value.CStatus != PdhCstatusNewData)
            return 0;

        var active = 100 - value.Value;
        return active < 0 ? 0 : active > 100 ? 100 : active;
    }

    /// <summary>Closes the PDH query handle. Safe to call more than once.</summary>
    public void Dispose() {
        if (_query != IntPtr.Zero)
            PdhCloseQuery(_query);
    }
}
