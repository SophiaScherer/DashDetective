using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace DashDetective.Services.SystemMetrics;

/// <summary>One physical GPU's reading, keyed by adapter token: its overall utilisation (the busiest engine,
/// 0–100) and a per-engine-type map whose value for each type is the busiest engine of that type, 0–100.
///
/// <b><see cref="Overall"/> is null when the adapter exists but its utilisation cannot be read</b> — the
/// state Linux needs for a card whose driver publishes no figure (the proprietary NVIDIA blob, Intel's
/// i915). It is not the same as absent: the inventory builds a GPU card only for an adapter this sampler
/// reports at all, so returning nothing hides the hardware, while returning 0 would show a real GPU as
/// permanently idle. Windows always fills it.
///
/// <see cref="DedicatedUsedBytes"/> is the adapter's dedicated video memory in use, or null when the
/// platform or driver reports none — never 0 for "unknown", since 0 is a real reading.</summary>
public sealed record GpuAdapterSample(
    double? Overall, IReadOnlyDictionary<string, double> Engines, ulong? DedicatedUsedBytes = null);

/// <summary>
/// Samples total GPU utilisation via the Windows PDH <c>\GPU Engine(*)\Utilization Percentage</c>
/// performance counter — the same source Task Manager uses, reported per physical adapter. No extra
/// dependencies beyond the OS <c>pdh.dll</c>; comparable per-sample cost to the CPU/Memory samplers.
///
/// Shared: the Dashboard and the Performance tab each own an instance. Moved here from
/// src/Tabs/Dashboard with sign-off when the Performance tab was activated — the same precedent as
/// <c>CpuUsageSampler</c> / <c>NetworkUsageSampler</c>. The platform check lives in
/// <see cref="IGpuUsageSampler.ForCurrentPlatform"/>.
///
/// <see cref="SampleAdapters"/> is the whole surface: the multi-GPU split moved every consumer onto it,
/// and the combined <c>Sample()</c> / <c>SampleEngines()</c> pair it replaced has been removed.
/// </summary>
internal sealed class WindowsGpuUsageSampler : IGpuUsageSampler {
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
    private const string MemoryCounterPath = @"\GPU Adapter Memory(*)\Dedicated Usage";

    private readonly IntPtr _query;
    private readonly IntPtr _counter;
    // Zero when the memory counter could not be added: utilisation then samples alone and usage stays null.
    private readonly IntPtr _memoryCounter;
    private readonly bool _ready;
    private bool _disposed;

    [SupportedOSPlatform("windows")]
    public WindowsGpuUsageSampler() {
        // A failure to stand up the query leaves _ready false; SampleAdapters() then returns an empty
        // map forever — the same soft-fail contract as the CPU/Memory samplers. The
        // catch covers pdh.dll failing to load or bind, which a return-code check can't see.
        try {
            if (PdhOpenQuery(null, IntPtr.Zero, out _query) != ErrorSuccess)
                return;

            if (PdhAddEnglishCounter(_query, CounterPath, IntPtr.Zero, out _counter) != ErrorSuccess) {
                PdhCloseQuery(_query);
                _query = IntPtr.Zero;
                return;
            }

            // A second counter on the same query, so one collect serves both. Its failure is not fatal.
            if (PdhAddEnglishCounter(_query, MemoryCounterPath, IntPtr.Zero, out _memoryCounter) != ErrorSuccess)
                _memoryCounter = IntPtr.Zero;

            // Seed one collect so the first sample reflects a real interval. The utilisation counter
            // is a rate that needs two data points, so priming here mirrors CpuUsageSampler seeding
            // GetSystemTimes in its constructor.
            PdhCollectQueryData(_query);
            _ready = true;
        } catch (Exception ex) when (NativeLoadFailure.Matches(ex)) {
            // An unwritten `out` leaves _query Zero, so Dispose stays a no-op.
            NativeLoadFailure.Report(nameof(WindowsGpuUsageSampler), ex);
        }
    }

    /// <summary>Test seam: skips native initialisation so the inert soft-fail contract can be exercised on
    /// a healthy host, where the real constructor always succeeds.</summary>
    internal WindowsGpuUsageSampler(SamplerInit _) { }

    /// <summary>
    /// Returns per-physical-GPU utilisation at the moment of the call, keyed by adapter LUID token
    /// (<c>luid_0x{High:x8}_0x{Low:x8}</c>, matching <see cref="WindowsGpuAdapterProvider"/>). Each
    /// <see cref="GpuAdapterSample"/> carries that adapter's overall % (busiest engine) and its
    /// per-engine-type map. Callers join the LUID keys against the inventory to attribute each reading to a
    /// named GPU. Any failure yields an empty map.
    /// </summary>
    public IReadOnlyDictionary<string, GpuAdapterSample> SampleAdapters() {
        // The disposed check comes first: collecting on a closed query would only fail its return code, so a
        // sampler someone else disposed would look identical to one with no GPU. Empty here is honest, and a
        // test can pin it.
        if (_disposed || !_ready || PdhCollectQueryData(_query) != ErrorSuccess)
            return EmptyAdapters;

        var engines = ReadCounter(_counter);
        if (engines is null)
            return EmptyAdapters;

        var samples = AggregateAdapters(engines);
        if (_memoryCounter == IntPtr.Zero || ReadCounter(_memoryCounter) is not { } memory)
            return samples;

        return GpuMemoryUsage.Join(samples, GpuMemoryUsage.ByAdapter(memory));
    }

    /// <summary>Reads every instance of one counter on the collected query, or null when PDH refuses.</summary>
    private static List<(string? Name, double Value)>? ReadCounter(IntPtr counter) {
        // First call sizes the buffer (returns PDH_MORE_DATA); the second fills it.
        uint bufferSize = 0;
        var status = PdhGetFormattedCounterArray(counter, PdhFmtDouble, ref bufferSize, out _, IntPtr.Zero);
        if (status != PdhMoreData || bufferSize == 0)
            return null;

        var buffer = Marshal.AllocHGlobal((int)bufferSize);
        try {
            if (PdhGetFormattedCounterArray(counter, PdhFmtDouble, ref bufferSize, out var itemCount, buffer) != ErrorSuccess)
                return null;

            return ReadItems(buffer, itemCount);
        } finally {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static readonly IReadOnlyDictionary<string, GpuAdapterSample> EmptyAdapters =
        new Dictionary<string, GpuAdapterSample>();

    /// <summary>Marshals a formatted counter array into (instance name, value) pairs.</summary>
    private static List<(string? Name, double Value)> ReadItems(IntPtr buffer, uint itemCount) {
        var itemSize = Marshal.SizeOf<CounterValueItem>();
        var items = new List<(string?, double)>((int)itemCount);
        for (var i = 0; i < itemCount; i++) {
            var item = Marshal.PtrToStructure<CounterValueItem>(buffer + i * itemSize);
            if (item.Name == IntPtr.Zero)
                continue;
            items.Add((Marshal.PtrToStringUni(item.Name), item.Value));
        }
        return items;
    }

    /// <summary>Applies Task Manager's per-engine rule (<see cref="GpuEngineLoad.ByAdapter"/>) to one
    /// read. Instances whose name does not parse are skipped.</summary>
    internal static IReadOnlyDictionary<string, GpuAdapterSample> AggregateAdapters(
        IEnumerable<(string? Name, double Value)> items) => GpuEngineLoad.ByAdapter(items);

    /// <summary>Closes the PDH query handle and leaves the sampler inert. Safe to call more than once — the
    /// flag also stops a second close on the same handle.</summary>
    public void Dispose() {
        if (_disposed)
            return;
        _disposed = true;

        if (_query != IntPtr.Zero)
            PdhCloseQuery(_query);
    }
}

/// <summary>The no-readings sampler — what a platform with no utilisation source gets. An empty map leaves
/// every GPU card at the value it was built with, which is the same "—" the old guard produced.</summary>
internal sealed class UnsupportedGpuUsageSampler : IGpuUsageSampler {
    private static readonly IReadOnlyDictionary<string, GpuAdapterSample> Empty =
        new Dictionary<string, GpuAdapterSample>();

    public IReadOnlyDictionary<string, GpuAdapterSample> SampleAdapters() => Empty;

    public void Dispose() { }
}
