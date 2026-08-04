using System;
using System.Runtime.InteropServices;

namespace DashDetective.Services.SystemMetrics;

/// <summary>
/// Samples the CPU's current clock as a percentage of its base clock, via the Windows PDH
/// <c>\Processor Information(_Total)\% Processor Performance</c> counter — the ratio Task Manager
/// multiplies by the base clock for its "Speed" figure. Deliberately NOT the
/// <c>% Processor Utility</c> counter <see cref="ProcessorUtilityCpuSampler"/> reads: Utility is
/// utilisation (roughly <c>% Processor Time × % Processor Performance ÷ 100</c>), so it collapses towards
/// zero at idle no matter how fast the cores are clocked. Single-instance counter, so it uses
/// <c>PdhGetFormattedCounterValue</c> like the utilisation sampler rather than the per-instance array.
/// Page-local to the Performance tab's CPU "Speed" tile; the shared CPU feed carries only the clamped
/// utilisation figure. A failure to stand up the query leaves it inert, returning 0 forever — the same
/// soft-fail contract as the other samplers. No dependencies beyond the OS <c>pdh.dll</c>.
/// </summary>
public sealed class ProcessorFrequencySampler : IDisposable {
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

    public ProcessorFrequencySampler() {
        // A failure to stand up the query leaves _ready false; Sample() then returns 0 forever and the
        // caller renders a placeholder. Mirrors the other page-local samplers' soft-fail contract. The
        // catch covers pdh.dll failing to load or bind, which a return-code check can't see.
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
            NativeLoadFailure.Report(nameof(ProcessorFrequencySampler), ex);
        }
    }

    /// <summary>Test seam: skips native initialisation so the inert soft-fail contract can be exercised on
    /// a healthy host, where the real constructor always succeeds.</summary>
    internal ProcessorFrequencySampler(SamplerInit _) { }

    /// <summary>
    /// Returns the current clock as a percentage of the base clock. Deliberately left unclamped above 100:
    /// the CPU boosts past its base clock under Turbo, which is precisely what the Speed readout shows.
    /// Any failure yields 0, which the caller treats as "no reading" (there is no genuine 0 % clock).
    /// </summary>
    public double Sample() {
        if (!_ready || PdhCollectQueryData(_query) != ErrorSuccess)
            return 0;

        if (PdhGetFormattedCounterValue(_counter, PdhFmtDouble, IntPtr.Zero, out var value) != ErrorSuccess)
            return 0;

        return value.Value < 0 ? 0 : value.Value;
    }

    /// <summary>Closes the PDH query handle. Safe to call more than once.</summary>
    public void Dispose() {
        if (_query != IntPtr.Zero) {
            PdhCloseQuery(_query);
            _query = IntPtr.Zero;
        }
    }
}
