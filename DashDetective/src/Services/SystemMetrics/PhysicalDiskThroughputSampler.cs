using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;

namespace DashDetective.Services.SystemMetrics;

/// <summary>Per-disk snapshot, keyed by disk number: read/write throughput (bytes per second), Task Manager's
/// disk "Active time" as a percentage (0–100, <c>100 − % Idle Time</c>), and the average transfer response
/// time in seconds.</summary>
public readonly record struct DiskThroughputSample(
    int DiskNumber, double ReadBytesPerSec, double WriteBytesPerSec, double ActivePercent, double ResponseSeconds);

/// <summary>
/// Samples per-disk read/write throughput and active time from the Windows PDH <c>\PhysicalDisk(*)\*</c>
/// counters. Unlike the shared <see cref="StorageUsageSampler"/> (which reads only the aggregate <c>_Total</c>
/// instance), this reads every disk instance at once via <c>PdhGetFormattedCounterArray</c> and keys each
/// reading by the disk number parsed from the instance name (e.g. "0 C:" → 0), so the Storage tab's per-disk
/// cards can show their own rates and the Dashboard's per-disk cards their own active time. Active time is
/// reported as <c>100 − % Idle Time</c> clamped 0–100, matching Task Manager (the same source as the aggregate
/// sampler). Page-local to the tab that owns it and driven by its own timer. A failure to stand up the query
/// leaves it inert, returning an empty set forever — the same soft-fail contract as the other samplers. No
/// dependencies beyond the OS <c>pdh.dll</c>.
/// </summary>
public sealed class PhysicalDiskThroughputSampler : IDisposable {
    // PDH status codes and formatting flags (winperf.h / pdhmsg.h).
    private const uint ErrorSuccess = 0x00000000;
    private const uint PdhMoreData = 0x800007D2;
    private const uint PdhFmtDouble = 0x00000200;
    private const uint PdhCstatusValidData = 0x00000000;
    private const uint PdhCstatusNewData = 0x00000001;

    private const string ReadPath = @"\PhysicalDisk(*)\Disk Read Bytes/sec";
    private const string WritePath = @"\PhysicalDisk(*)\Disk Write Bytes/sec";
    // Active time is derived from idle time (the % Disk Time counter can read above 100% under load), matching
    // StorageUsageSampler's aggregate reading.
    private const string IdlePath = @"\PhysicalDisk(*)\% Idle Time";
    private const string ResponsePath = @"\PhysicalDisk(*)\Avg. Disk sec/Transfer";

    /// <summary>
    /// One item of a formatted counter array — a <c>PDH_FMT_COUNTERVALUE_ITEM</c>: the instance name pointer
    /// (into the same buffer) followed by the value struct. The runtime inserts 4 bytes of padding after
    /// <see cref="CStatus"/> so <see cref="Value"/> lands on an 8-byte boundary.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct CounterValueItem {
        public IntPtr SzName;
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

    private readonly IntPtr _query;
    private readonly IntPtr _readCounter;
    private readonly IntPtr _writeCounter;
    private readonly IntPtr _idleCounter;
    private readonly IntPtr _responseCounter;
    private readonly bool _ready;

    public PhysicalDiskThroughputSampler() {
        // A failure to stand up the query leaves _ready false; Sample() then returns an empty set forever.
        if (PdhOpenQuery(null, IntPtr.Zero, out _query) != ErrorSuccess)
            return;

        if (PdhAddEnglishCounter(_query, ReadPath, IntPtr.Zero, out _readCounter) != ErrorSuccess
            || PdhAddEnglishCounter(_query, WritePath, IntPtr.Zero, out _writeCounter) != ErrorSuccess
            || PdhAddEnglishCounter(_query, IdlePath, IntPtr.Zero, out _idleCounter) != ErrorSuccess
            || PdhAddEnglishCounter(_query, ResponsePath, IntPtr.Zero, out _responseCounter) != ErrorSuccess) {
            PdhCloseQuery(_query);
            _query = IntPtr.Zero;
            return;
        }

        // Seed one collect so the first Sample() reflects a real interval (these are rate counters needing
        // two data points), mirroring StorageUsageSampler priming its query.
        PdhCollectQueryData(_query);
        _ready = true;
    }

    /// <summary>Returns per-disk read/write throughput at the moment of the call, one entry per physical
    /// disk instance. Any failure yields an empty set.</summary>
    public IReadOnlyList<DiskThroughputSample> Sample() {
        if (!_ready || PdhCollectQueryData(_query) != ErrorSuccess)
            return Array.Empty<DiskThroughputSample>();

        var reads = ReadArray(_readCounter);
        var writes = ReadArray(_writeCounter);
        var idles = ReadArray(_idleCounter);
        var responses = ReadArray(_responseCounter);

        var samples = new List<DiskThroughputSample>(reads.Count);
        foreach (var (disk, read) in reads) {
            writes.TryGetValue(disk, out var write);
            responses.TryGetValue(disk, out var response);
            // Only report active time when idle time was actually read; a missing reading would otherwise
            // masquerade as 100% active (100 − 0).
            var active = idles.TryGetValue(disk, out var idle) ? Math.Clamp(100 - idle, 0, 100) : 0;
            samples.Add(new DiskThroughputSample(disk, read, write, active, response));
        }
        return samples;
    }

    /// <summary>Reads one wildcard counter into a disk-number → value map. Skips the "_Total" aggregate and
    /// any instance whose name doesn't begin with a disk number, and any item with an invalid status.</summary>
    private static Dictionary<int, double> ReadArray(IntPtr counter) {
        var map = new Dictionary<int, double>();

        // First call (null buffer) reports the required buffer size via PDH_MORE_DATA.
        uint bufferSize = 0;
        if (PdhGetFormattedCounterArray(counter, PdhFmtDouble, ref bufferSize, out _, IntPtr.Zero) != PdhMoreData
            || bufferSize == 0)
            return map;

        var buffer = Marshal.AllocHGlobal((int)bufferSize);
        try {
            if (PdhGetFormattedCounterArray(counter, PdhFmtDouble, ref bufferSize, out var itemCount, buffer)
                != ErrorSuccess)
                return map;

            var itemSize = Marshal.SizeOf<CounterValueItem>();
            for (var i = 0; i < itemCount; i++) {
                var item = Marshal.PtrToStructure<CounterValueItem>(buffer + i * itemSize);
                if (item.CStatus != PdhCstatusValidData && item.CStatus != PdhCstatusNewData)
                    continue;

                var name = Marshal.PtrToStringUni(item.SzName);
                if (name is null || !TryParseDiskNumber(name, out var disk))
                    continue;

                map[disk] = item.Value < 0 ? 0 : item.Value;
            }
        } finally {
            Marshal.FreeHGlobal(buffer);
        }

        return map;
    }

    /// <summary>Parses the leading disk number from a PhysicalDisk instance name like "0 C:" or "1".
    /// Returns false for "_Total" or any name that doesn't start with a number.</summary>
    private static bool TryParseDiskNumber(string instance, out int disk) {
        var space = instance.IndexOf(' ');
        var head = space >= 0 ? instance[..space] : instance;
        return int.TryParse(head, NumberStyles.Integer, CultureInfo.InvariantCulture, out disk);
    }

    /// <summary>Closes the PDH query handle. Safe to call more than once.</summary>
    public void Dispose() {
        if (_query != IntPtr.Zero)
            PdhCloseQuery(_query);
    }
}
