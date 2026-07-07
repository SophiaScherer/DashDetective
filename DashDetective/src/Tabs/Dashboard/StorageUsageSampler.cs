using System;
using System.Runtime.InteropServices;

namespace DashDetective.Tabs.Dashboard;

/// <summary>One live disk reading: activity percentage plus average response time.</summary>
/// <param name="ActivePercent">Physical-disk busy time, 0–100 (drives the sparkline).</param>
/// <param name="ResponseMs">Average time per transfer, in milliseconds (the card headline).</param>
public readonly record struct StorageSample(double ActivePercent, double ResponseMs);

/// <summary>
/// Samples total physical-disk metrics via Windows PDH on the aggregate <c>_Total</c> instance:
/// <list type="bullet">
/// <item><c>\PhysicalDisk(_Total)\% Idle Time</c> → reported as <c>active = 100 − idle</c>, how Task
/// Manager derives disk "Active time" (the <c>% Disk Time</c> counter can exceed 100% under load,
/// so idle time is the reliable source).</item>
/// <item><c>\PhysicalDisk(_Total)\Avg. Disk sec/Transfer</c> → seconds per transfer, scaled to ms —
/// Task Manager's "Average response time".</item>
/// </list>
/// Both counters live on one query. Each <see cref="Sample"/> returns a <see cref="StorageSample"/>.
/// No extra dependencies beyond the OS <c>pdh.dll</c>; comparable per-sample cost to the CPU/GPU
/// samplers.
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

    private const string IdleCounterPath = @"\PhysicalDisk(_Total)\% Idle Time";
    private const string TransferCounterPath = @"\PhysicalDisk(_Total)\Avg. Disk sec/Transfer";

    private readonly IntPtr _query;
    private readonly IntPtr _idleCounter;
    private readonly IntPtr _transferCounter;
    private readonly bool _ready;

    public StorageUsageSampler() {
        // A failure to stand up the query leaves _ready false; Sample() then returns a zero snapshot
        // forever and the caller stops its timer — the same soft-fail contract as the CPU/Memory/GPU
        // samplers.
        if (PdhOpenQuery(null, IntPtr.Zero, out _query) != ErrorSuccess)
            return;

        if (PdhAddEnglishCounter(_query, IdleCounterPath, IntPtr.Zero, out _idleCounter) != ErrorSuccess ||
            PdhAddEnglishCounter(_query, TransferCounterPath, IntPtr.Zero, out _transferCounter) != ErrorSuccess) {
            PdhCloseQuery(_query);
            _query = IntPtr.Zero;
            return;
        }

        // Seed one collect so the first Sample() reflects a real interval. Both counters are rates
        // that need two data points, so priming here mirrors GpuUsageSampler seeding its query.
        PdhCollectQueryData(_query);
        _ready = true;
    }

    /// <summary>
    /// Returns the current disk activity (0–100) and average response time (ms) on the aggregate
    /// <c>_Total</c> instance. Any failure yields a zero snapshot.
    /// </summary>
    public StorageSample Sample() {
        if (!_ready || PdhCollectQueryData(_query) != ErrorSuccess)
            return default;

        return new StorageSample(ReadActivePercent(), ReadResponseMs());
    }

    /// <summary>Reads <c>% Idle Time</c> and returns <c>100 − idle</c>, clamped 0–100. Failure → 0.</summary>
    private double ReadActivePercent() {
        if (!TryRead(_idleCounter, out var idle))
            return 0;

        var active = 100 - idle;
        return active < 0 ? 0 : active > 100 ? 100 : active;
    }

    /// <summary>Reads <c>Avg. Disk sec/Transfer</c> and scales to milliseconds. Failure → 0.</summary>
    private double ReadResponseMs() {
        if (!TryRead(_transferCounter, out var seconds))
            return 0;

        var ms = seconds * 1000;
        return ms < 0 ? 0 : ms;
    }

    /// <summary>Formats a single counter as a double, guarding the PDH status word.</summary>
    private static bool TryRead(IntPtr counter, out double value) {
        if (PdhGetFormattedCounterValue(counter, PdhFmtDouble, out _, out var formatted) != ErrorSuccess ||
            (formatted.CStatus != PdhCstatusValidData && formatted.CStatus != PdhCstatusNewData)) {
            value = 0;
            return false;
        }

        value = formatted.Value;
        return true;
    }

    /// <summary>Closes the PDH query handle. Safe to call more than once.</summary>
    public void Dispose() {
        if (_query != IntPtr.Zero)
            PdhCloseQuery(_query);
    }
}
