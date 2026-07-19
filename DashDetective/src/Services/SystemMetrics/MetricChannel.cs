using Avalonia.Threading;
using System;

namespace DashDetective.Services.SystemMetrics;

/// <summary>
/// A reusable "sampler + <see cref="DispatcherTimer"/> + rolling <c>double[window]</c> history" unit —
/// the pattern that was copy-pasted per metric across the Dashboard and Performance view models. Each
/// tick reads one <typeparamref name="TSample"/> from <c>sample</c>, appends a scalar projection of it
/// to a fixed-width rolling window, and hands the full sample to <c>onSample</c> for the view model to
/// render.
///
/// Design choices mirror the previous inline pattern exactly, so behaviour is unchanged:
/// <list type="bullet">
/// <item><b>One timer per channel</b> — metrics stay independent, so one sampler failing stops only its
/// own channel and never disturbs the others (the fault-isolation contract).</item>
/// <item><b>One try/catch per tick</b> — a sampler exception calls <c>onFailed</c> (which sets the
/// view's neutral placeholder) and <b>permanently stops</b> the timer, rather than throwing on the UI
/// thread every interval.</item>
/// <item><b>Seed-then-start</b> — the constructor does not auto-start; callers seed the first frame
/// (flat-at-zero history) then call <see cref="Start"/>, matching the old constructors.</item>
/// <item><b>SampleNow decoupled from the timer</b> — the toolbar Refresh must update once even while
/// paused, so <see cref="SampleNow"/> runs a tick regardless of timer state.</item>
/// </list>
///
/// The channel owns a single rolling history. The one metric that needs two histories from one sample
/// (network download + upload) uses this generic channel for the primary buffer and pushes the second
/// buffer itself via <see cref="PushHistory"/> — so there is still exactly one shift implementation and
/// no raw <c>Array.Copy</c> left in the view models.
/// </summary>
/// <typeparam name="TSample">The value returned by one sampler call (a plain <c>double</c>, or a
/// snapshot record such as <c>MemorySample</c>/<c>StorageSample</c>/<c>NetworkSample</c>).</typeparam>
public class MetricChannel<TSample> : IDisposable {
    private readonly DispatcherTimer _timer;
    private readonly Func<TSample> _sample;
    private readonly Func<TSample, double> _historyValue;
    private readonly Action<TSample> _onSample;
    private readonly Action _onFailed;
    private readonly double[] _history;

    /// <param name="interval">Tick cadence (e.g. one second for the live metrics).</param>
    /// <param name="windowSize">Length of the rolling history, in samples.</param>
    /// <param name="sample">Reads one sample; may throw when the underlying counter is unavailable.</param>
    /// <param name="historyValue">Projects the scalar that goes into the rolling window (identity for a
    /// plain <c>double</c>; e.g. <c>s =&gt; s.LoadPercent</c> for a memory snapshot).</param>
    /// <param name="onSample">Applies a successful sample to the view (reads <see cref="History"/> to
    /// rebuild the chart). Runs on the UI thread, after the history has been updated.</param>
    /// <param name="onFailed">Runs once when <paramref name="sample"/> throws — sets the view's neutral
    /// placeholder. The timer is stopped immediately afterwards.</param>
    public MetricChannel(TimeSpan interval, int windowSize, Func<TSample> sample,
                         Func<TSample, double> historyValue, Action<TSample> onSample, Action onFailed) {
        _history = new double[windowSize];
        _sample = sample;
        _historyValue = historyValue;
        _onSample = onSample;
        _onFailed = onFailed;
        _timer = new DispatcherTimer { Interval = interval };
        _timer.Tick += OnTick;
    }

    /// <summary>No-history variant: timer + sampling + fan-out only, for consumers that keep no rolling
    /// buffer (the shared <see cref="SystemMetricsService"/>, which fans each sample out to subscribers
    /// that own their own histories).</summary>
    public MetricChannel(TimeSpan interval, Func<TSample> sample, Action<TSample> onSample, Action onFailed)
        : this(interval, 0, sample, static _ => 0.0, onSample, onFailed) { }

    /// <summary>The rolling history, oldest-first. A read-only view over the live buffer; valid only for
    /// synchronous reads on the UI thread (the next tick mutates it in place).</summary>
    public ReadOnlySpan<double> History => _history;

    /// <summary>Starts (or resumes) periodic sampling. Drives the shell's Live pill.</summary>
    public void Start() => _timer.Start();

    /// <summary>Pauses periodic sampling. Drives the shell's Live pill; <see cref="SampleNow"/> still
    /// works while stopped.</summary>
    public void Stop() => _timer.Stop();

    /// <summary>Samples once immediately, regardless of whether the timer is running — for the toolbar
    /// Refresh, which must update once even while paused. On a sampler failure this re-invokes
    /// <c>onFailed</c> and re-stops the timer, exactly as a live tick would (harmless).</summary>
    public void SampleNow() => Tick();

    private void OnTick(object? sender, EventArgs e) => Tick();

    private void Tick() {
        TSample value;
        try {
            value = _sample();
        } catch {
            // Sampling is unavailable (e.g. a non-Windows host or a missing counter). Show the view's
            // neutral placeholder and stop polling rather than throwing on the UI thread every interval.
            _onFailed();
            _timer.Stop();
            return;
        }

        // Shift the window left by one and append the newest sample at the end (skipped for no-history channels).
        if (_history.Length > 0)
            PushHistory(_history, _historyValue(value));
        _onSample(value);
    }

    /// <summary>The single canonical rolling-window update: shift <paramref name="buffer"/> left by one
    /// and append <paramref name="value"/> at the end. Public so the two-history network case reuses the
    /// exact same shift for its second buffer instead of re-inlining <c>Array.Copy</c>.</summary>
    public static void PushHistory(double[] buffer, double value) {
        if (buffer.Length == 0)
            return;
        Array.Copy(buffer, 1, buffer, 0, buffer.Length - 1);
        buffer[^1] = value;
    }

    /// <summary>Stops the timer and unsubscribes the tick handler. Safe to call more than once.</summary>
    public void Dispose() {
        _timer.Stop();
        _timer.Tick -= OnTick;
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Non-generic convenience for the common case: a metric whose sampler returns a plain <c>double</c>
/// that is both the value shown and the value charted (CPU and GPU utilisation). The rolling-window
/// projection is the identity, so callers pass only the sampler and the two callbacks.
/// </summary>
public sealed class MetricChannel : MetricChannel<double> {
    /// <param name="interval">Tick cadence.</param>
    /// <param name="windowSize">Length of the rolling history, in samples.</param>
    /// <param name="sample">Reads one utilisation percentage; may throw when unavailable.</param>
    /// <param name="onSample">Applies a successful sample to the view.</param>
    /// <param name="onFailed">Sets the view's neutral placeholder on a sampler failure.</param>
    public MetricChannel(TimeSpan interval, int windowSize, Func<double> sample,
                         Action<double> onSample, Action onFailed)
        : base(interval, windowSize, sample, static v => v, onSample, onFailed) { }
}
