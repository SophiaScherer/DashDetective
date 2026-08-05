using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace DashDetective.Services.SystemMetrics;

/// <summary>
/// Samples the CPU's current clock as a percentage of its base clock, via the Windows PDH
/// <c>\Processor Information(_Total)\% Processor Performance</c> counter — the ratio Task Manager
/// multiplies by the base clock for its "Speed" figure. Deliberately NOT the
/// <c>% Processor Utility</c> counter <see cref="ProcessorUtilityCpuSampler"/> reads: Utility is
/// utilisation (roughly <c>% Processor Time × % Processor Performance ÷ 100</c>), so it collapses towards
/// zero at idle no matter how fast the cores are clocked. Single-instance counter, so it uses
/// <c>PdhGetFormattedCounterValue</c> like the utilisation sampler rather than the per-instance array.
/// A failure to stand up the query leaves it inert, returning a default sample forever — the same
/// soft-fail contract as the other samplers. No dependencies beyond the OS <c>pdh.dll</c>.
/// </summary>
internal sealed class WindowsProcessorFrequencySampler : IProcessorFrequencySampler {
    // PDH status codes and formatting flags (pdhmsg.h / winperf.h).
    private const uint ErrorSuccess = 0x00000000;
    private const uint PdhFmtDouble = 0x00000200;

    private const string CounterPath = @"\Processor Information(_Total)\% Processor Performance";

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

    private IntPtr _query;
    private readonly IntPtr _counter;
    private readonly bool _ready;

    /// <summary>Stands up the PDH query. Annotated rather than the whole type so <see cref="Sample"/> and
    /// <see cref="Dispose"/> stay callable from tests on every platform — the same shape as the other
    /// PDH samplers.</summary>
    [SupportedOSPlatform("windows")]
    public WindowsProcessorFrequencySampler() {
        // A failure to stand up the query leaves _ready false; Sample() then returns a default sample
        // forever and the caller renders a placeholder. Mirrors the other page-local samplers' soft-fail
        // contract. The catch covers pdh.dll failing to load or bind, which a return-code check can't see.
        try {
            if (PdhOpenQuery(null, IntPtr.Zero, out _query) != ErrorSuccess)
                return;

            if (PdhAddEnglishCounter(_query, CounterPath, IntPtr.Zero, out _counter) != ErrorSuccess) {
                PdhCloseQuery(_query);
                _query = IntPtr.Zero;
                return;
            }

            // Seed one collect so the first Sample() reflects a real interval — this is a ratio counter that
            // needs two data points, like the utilisation counter.
            PdhCollectQueryData(_query);
            _ready = true;
        } catch (Exception ex) when (NativeLoadFailure.Matches(ex)) {
            // An unwritten `out` leaves _query Zero, so Dispose stays a no-op.
            NativeLoadFailure.Report(nameof(WindowsProcessorFrequencySampler), ex);
        }
    }

    /// <summary>Test seam: skips native initialisation so the inert soft-fail contract can be exercised on
    /// a healthy host, where the real constructor always succeeds.</summary>
    internal WindowsProcessorFrequencySampler(SamplerInit _) { }

    /// <summary>
    /// Returns the current clock as a percentage of the base clock. Deliberately left unclamped above 100:
    /// the CPU boosts past its base clock under Turbo, which is precisely what the Speed readout shows.
    /// Any failure yields a default sample, which the caller treats as "no reading" (there is no genuine
    /// 0 % clock).
    /// </summary>
    public ProcessorClockSample Sample() {
        if (!_ready || PdhCollectQueryData(_query) != ErrorSuccess)
            return default;

        if (PdhGetFormattedCounterValue(_counter, PdhFmtDouble, IntPtr.Zero, out var value) != ErrorSuccess)
            return default;

        // PDH gives a ratio, never an absolute clock — the base clock comes from the static CPU info.
        return new ProcessorClockSample(value.Value < 0 ? 0 : value.Value, AbsoluteMhz: 0);
    }

    /// <summary>Closes the PDH query handle. Safe to call more than once.</summary>
    public void Dispose() {
        if (_query != IntPtr.Zero) {
            PdhCloseQuery(_query);
            _query = IntPtr.Zero;
        }
    }
}

/// <summary>The no-data arm: a platform with no clock reader yet reports nothing, so the Speed tile keeps
/// the "—" it was built with.</summary>
internal sealed class UnsupportedProcessorFrequencySampler : IProcessorFrequencySampler {
    public ProcessorClockSample Sample() => default;

    public void Dispose() { }
}
