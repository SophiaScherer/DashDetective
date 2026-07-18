using System;
using System.Runtime.InteropServices;

namespace DashDetective.Services.SystemMetrics;

/// <summary>
/// A single physical-disk snapshot on the aggregate <c>_Total</c> instance: activity as a percentage
/// (0–100), read/write throughput in bytes per second, and average transfer response time in seconds.
/// </summary>
public readonly record struct StorageSample(
    double ActivePercent, double ReadBytesPerSec, double WriteBytesPerSec, double ResponseSeconds);

/// <summary>
/// Samples total physical-disk metrics via Windows PDH <c>\PhysicalDisk(_Total)\*</c> performance
/// counters. Activity is reported as <c>active = 100 − % Idle Time</c>, clamped 0–100 — Task Manager's
/// disk "Active time" (the <c>% Disk Time</c> counter can read above 100% under load, so idle time is
/// the reliable source) — alongside read/write throughput and average response time. Each
/// <see cref="Sample"/> call returns the current snapshot. No extra dependencies beyond the OS
/// <c>pdh.dll</c>; comparable per-sample cost to the CPU/GPU samplers.
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

    private const string IdlePath = @"\PhysicalDisk(_Total)\% Idle Time";
    private const string ReadPath = @"\PhysicalDisk(_Total)\Disk Read Bytes/sec";
    private const string WritePath = @"\PhysicalDisk(_Total)\Disk Write Bytes/sec";
    private const string ResponsePath = @"\PhysicalDisk(_Total)\Avg. Disk sec/Transfer";

    private readonly IntPtr _query;
    private readonly IntPtr _idleCounter;
    private readonly IntPtr _readCounter;
    private readonly IntPtr _writeCounter;
    private readonly IntPtr _responseCounter;
    private readonly bool _ready;

    public StorageUsageSampler() {
        // A failure to stand up the query leaves _ready false; Sample() then returns a zero snapshot
        // forever and the caller stops its timer — the same soft-fail contract as the other samplers.
        if (PdhOpenQuery(null, IntPtr.Zero, out _query) != ErrorSuccess)
            return;

        if (!AddCounter(IdlePath, out _idleCounter)
            || !AddCounter(ReadPath, out _readCounter)
            || !AddCounter(WritePath, out _writeCounter)
            || !AddCounter(ResponsePath, out _responseCounter)) {
            PdhCloseQuery(_query);
            _query = IntPtr.Zero;
            return;
        }

        // Seed one collect so the first Sample() reflects a real interval. These are all rate/average
        // counters that need two data points, so priming here mirrors GpuUsageSampler seeding its query.
        PdhCollectQueryData(_query);
        _ready = true;
    }

    private bool AddCounter(string path, out IntPtr counter) =>
        PdhAddEnglishCounter(_query, path, IntPtr.Zero, out counter) == ErrorSuccess;

    /// <summary>
    /// Returns the total physical-disk snapshot at the moment of the call, on the aggregate
    /// <c>_Total</c> instance. Any failure yields a zero snapshot.
    /// </summary>
    public StorageSample Sample() {
        if (!_ready || PdhCollectQueryData(_query) != ErrorSuccess)
            return default;

        var idle = ReadCounter(_idleCounter);
        var active = 100 - idle;
        active = active < 0 ? 0 : active > 100 ? 100 : active;

        return new StorageSample(
            active, ReadCounter(_readCounter), ReadCounter(_writeCounter), ReadCounter(_responseCounter));
    }

    /// <summary>Reads one formatted counter as a non-negative double, or 0 on any failure/invalid status.</summary>
    private static double ReadCounter(IntPtr counter) {
        if (PdhGetFormattedCounterValue(counter, PdhFmtDouble, out _, out var value) != ErrorSuccess)
            return 0;
        if (value.CStatus != PdhCstatusValidData && value.CStatus != PdhCstatusNewData)
            return 0;
        return value.Value < 0 ? 0 : value.Value;
    }

    /// <summary>Closes the PDH query handle. Safe to call more than once.</summary>
    public void Dispose() {
        if (_query != IntPtr.Zero)
            PdhCloseQuery(_query);
    }
}
