using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace DashDetective.Services.SystemMetrics;

/// <summary>One physical GPU's reading, keyed by adapter token: its overall utilisation (the busiest engine
/// type, 0–100) and the per-engine-type map behind it (raw sums, clamped by the caller for display).
///
/// <b><see cref="Overall"/> is null when the adapter exists but its utilisation cannot be read</b> — the
/// state Linux needs for a card whose driver publishes no figure (the proprietary NVIDIA blob, Intel's
/// i915). It is not the same as absent: the inventory builds a GPU card only for an adapter this sampler
/// reports at all, so returning nothing hides the hardware, while returning 0 would show a real GPU as
/// permanently idle. Windows always fills it.</summary>
public sealed record GpuAdapterSample(double? Overall, IReadOnlyDictionary<string, double> Engines);

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

    private readonly IntPtr _query;
    private readonly IntPtr _counter;
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
    /// <see cref="GpuAdapterSample"/> carries that adapter's overall % (busiest engine type) and its
    /// per-engine-type map. Callers join the LUID keys against the inventory to attribute each reading to a
    /// named GPU. Any failure yields an empty map.
    /// </summary>
    public IReadOnlyDictionary<string, GpuAdapterSample> SampleAdapters() {
        // The disposed check comes first: collecting on a closed query would only fail its return code, so a
        // sampler someone else disposed would look identical to one with no GPU. Empty here is honest, and a
        // test can pin it.
        if (_disposed || !_ready || PdhCollectQueryData(_query) != ErrorSuccess)
            return EmptyAdapters;

        // First call sizes the buffer (returns PDH_MORE_DATA); the second fills it.
        uint bufferSize = 0;
        var status = PdhGetFormattedCounterArray(_counter, PdhFmtDouble, ref bufferSize, out _, IntPtr.Zero);
        if (status != PdhMoreData || bufferSize == 0)
            return EmptyAdapters;

        var buffer = Marshal.AllocHGlobal((int)bufferSize);
        try {
            if (PdhGetFormattedCounterArray(_counter, PdhFmtDouble, ref bufferSize, out var itemCount, buffer) != ErrorSuccess)
                return EmptyAdapters;

            return AggregateAdapters(ReadItems(buffer, itemCount));
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

    /// <summary>
    /// Groups counter instances by adapter LUID then engine type, summing within each engine and taking the
    /// busiest engine type as the adapter's overall % (clamped 0–100). Pure (no PDH/marshalling) so it is
    /// unit-tested directly. Instances that carry no LUID or engine token are skipped.
    /// </summary>
    internal static IReadOnlyDictionary<string, GpuAdapterSample> AggregateAdapters(
        IEnumerable<(string? Name, double Value)> items) {
        var perAdapter = new Dictionary<string, Dictionary<string, double>>(StringComparer.Ordinal);

        foreach (var (name, value) in items) {
            var luid = ParseLuidToken(name);
            var engine = EngineType(name);
            if (luid is null || engine is null)
                continue;

            if (!perAdapter.TryGetValue(luid, out var engines))
                perAdapter[luid] = engines = new Dictionary<string, double>(StringComparer.Ordinal);
            engines.TryGetValue(engine, out var running);
            engines[engine] = running + value;
        }

        var result = new Dictionary<string, GpuAdapterSample>(StringComparer.Ordinal);
        foreach (var (luid, engines) in perAdapter) {
            double max = 0;
            foreach (var total in engines.Values)
                if (total > max)
                    max = total;
            result[luid] = new GpuAdapterSample(max < 0 ? 0 : max > 100 ? 100 : max, engines);
        }
        return result;
    }

    /// <summary>Extracts the adapter LUID token (<c>luid_0x…_0x…</c>, lower-cased) from an instance name like
    /// <c>pid_1234_luid_0x00000000_0x0000e54b_phys_0_eng_0_engtype_3D</c>, or null when absent.</summary>
    internal static string? ParseLuidToken(string? instanceName) {
        if (string.IsNullOrEmpty(instanceName))
            return null;

        const string token = "luid_";
        var start = instanceName.IndexOf(token, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
            return null;

        // The LUID token is followed by the "_phys" segment; slice between them (falling back to end of
        // string) and normalise casing so it joins the DXGI-formatted token regardless of PDH's casing.
        var phys = instanceName.IndexOf("_phys", start, StringComparison.OrdinalIgnoreCase);
        var end = phys > start ? phys : instanceName.Length;
        return instanceName[start..end].ToLowerInvariant();
    }

    /// <summary>Extracts the engine type after the trailing <c>engtype_</c> token, or null.</summary>
    private static string? EngineType(string? instanceName) {
        if (string.IsNullOrEmpty(instanceName))
            return null;

        const string token = "engtype_";
        var idx = instanceName.LastIndexOf(token, StringComparison.Ordinal);
        return idx < 0 ? null : instanceName[(idx + token.Length)..];
    }

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
