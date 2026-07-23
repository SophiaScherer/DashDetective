using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;

namespace DashDetective.Services.SystemMetrics;

/// <summary>One logical processor's utilisation: its PDH instance name ("group,core", e.g. "0,3"), the parsed
/// group/core numbers, and the current utilisation percentage.</summary>
public readonly record struct LogicalProcessorSample(string Instance, int Group, int Core, double Percent);

/// <summary>
/// Samples per-logical-processor utilisation from the Windows PDH <c>\Processor Information(*)\% Processor
/// Utility</c> counters — the per-core form of the Task-Manager-matching aggregate the
/// <see cref="ProcessorUtilityCpuSampler"/> reads. Reads every instance at once via
/// <c>PdhGetFormattedCounterArray</c>, drops the <c>_Total</c> roll-ups, and returns one reading per logical
/// processor ordered by (group, core). Page-local to the Performance tab's CPU "Detailed" view; drives one mini
/// chart per logical processor. A failure to stand up the query leaves it inert, returning an empty set forever —
/// the same soft-fail contract as the other samplers. No dependencies beyond the OS <c>pdh.dll</c>.
/// </summary>
public sealed class LogicalProcessorSampler : IDisposable {
    // PDH status codes and formatting flags (winperf.h / pdhmsg.h).
    private const uint ErrorSuccess = 0x00000000;
    private const uint PdhMoreData = 0x800007D2;
    private const uint PdhFmtDouble = 0x00000200;
    private const uint PdhCstatusValidData = 0x00000000;
    private const uint PdhCstatusNewData = 0x00000001;

    private const string CounterPath = @"\Processor Information(*)\% Processor Utility";

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
    private readonly IntPtr _counter;
    private readonly bool _ready;

    public LogicalProcessorSampler() {
        // A failure to stand up the query leaves _ready false; Sample() then returns an empty set forever.
        if (PdhOpenQuery(null, IntPtr.Zero, out _query) != ErrorSuccess)
            return;

        if (PdhAddEnglishCounter(_query, CounterPath, IntPtr.Zero, out _counter) != ErrorSuccess) {
            PdhCloseQuery(_query);
            _query = IntPtr.Zero;
            return;
        }

        // Seed one collect so the first Sample() reflects a real interval (this is a rate counter needing two
        // data points), mirroring the other array samplers.
        PdhCollectQueryData(_query);
        _ready = true;
    }

    /// <summary>Returns one reading per logical processor at the moment of the call, ordered by (group, core)
    /// with the <c>_Total</c> roll-ups dropped. Any failure yields an empty set.</summary>
    public IReadOnlyList<LogicalProcessorSample> Sample() {
        if (!_ready || PdhCollectQueryData(_query) != ErrorSuccess)
            return Array.Empty<LogicalProcessorSample>();

        // First call (null buffer) reports the required buffer size via PDH_MORE_DATA.
        uint bufferSize = 0;
        if (PdhGetFormattedCounterArray(_counter, PdhFmtDouble, ref bufferSize, out _, IntPtr.Zero) != PdhMoreData
            || bufferSize == 0)
            return Array.Empty<LogicalProcessorSample>();

        var buffer = Marshal.AllocHGlobal((int)bufferSize);
        try {
            if (PdhGetFormattedCounterArray(_counter, PdhFmtDouble, ref bufferSize, out var itemCount, buffer)
                != ErrorSuccess)
                return Array.Empty<LogicalProcessorSample>();

            var samples = new List<LogicalProcessorSample>((int)itemCount);
            var itemSize = Marshal.SizeOf<CounterValueItem>();
            for (var i = 0; i < itemCount; i++) {
                var item = Marshal.PtrToStructure<CounterValueItem>(buffer + i * itemSize);
                if (item.CStatus != PdhCstatusValidData && item.CStatus != PdhCstatusNewData)
                    continue;

                var name = Marshal.PtrToStringUni(item.SzName);
                if (!TryParseInstance(name, out var group, out var core))
                    continue;

                samples.Add(new LogicalProcessorSample(name!, group, core, item.Value < 0 ? 0 : item.Value));
            }

            samples.Sort(static (a, b) => a.Group != b.Group ? a.Group.CompareTo(b.Group) : a.Core.CompareTo(b.Core));
            return samples;
        } finally {
            Marshal.FreeHGlobal(buffer);
        }
    }

    /// <summary>Parses a "group,core" instance name (e.g. "0,3") into its numeric parts. Returns false for the
    /// "_Total" roll-ups ("0,_Total", "_Total") and any name that isn't two comma-separated integers.</summary>
    internal static bool TryParseInstance(string? instance, out int group, out int core) {
        group = 0;
        core = 0;
        if (string.IsNullOrEmpty(instance))
            return false;

        var comma = instance.IndexOf(',');
        if (comma <= 0 || comma == instance.Length - 1)
            return false;

        return int.TryParse(instance.AsSpan(0, comma), NumberStyles.Integer, CultureInfo.InvariantCulture, out group)
            && int.TryParse(instance.AsSpan(comma + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out core);
    }

    /// <summary>Closes the PDH query handle. Safe to call more than once.</summary>
    public void Dispose() {
        if (_query != IntPtr.Zero)
            PdhCloseQuery(_query);
    }
}
