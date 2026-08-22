using DashDetective.Services.Diagnostics;
using DashDetective.Services.Threading;
using DashDetective.Shared.Charts;
using System;

namespace DashDetective.Services.SystemMetrics;

/// <summary>
/// Reusable "sampler + <c>DispatcherTimer</c> + rolling <see cref="MetricHistory"/>" unit,
/// replacing the per-metric timer/buffer pattern once copy-pasted across the view models. Each tick
/// samples once, appends a scalar projection to the window, and hands the full sample to <c>onSample</c>.
/// A sampler exception calls <c>onFailed</c> and permanently stops the timer (per-channel fault
/// isolation); <see cref="SampleNow"/> samples once regardless of timer state (for paused Refresh). The
/// window is a <see cref="MetricHistory"/>, so a consumer can tell a real zero from a slot no sample has
/// reached yet.
/// </summary>
/// <typeparam name="TSample">One sampler call's result — a <c>double</c> or a snapshot record.</typeparam>
public class MetricChannel<TSample> : IDisposable {
    private readonly IUiTimer _timer;
    private readonly Func<TSample> _sample;
    private readonly Func<TSample, double> _historyValue;
    private readonly Action<TSample> _onSample;
    private readonly Action _onFailed;
    private readonly MetricHistory _history;

    /// <param name="historyValue">Projects the scalar pushed into the window (identity for a plain
    /// <c>double</c>; e.g. <c>s =&gt; s.LoadPercent</c> for a snapshot).</param>
    public MetricChannel(TimeSpan interval, int windowSize, Func<TSample> sample,
                         Func<TSample, double> historyValue, Action<TSample> onSample, Action onFailed)
        : this(interval, windowSize, sample, historyValue, onSample, onFailed, new DispatcherTimerAdapter()) { }

    /// <summary>Test seam: takes the timer explicitly. A real <c>DispatcherTimer</c> only fires while an
    /// Avalonia dispatcher is pumping, so headless unit tests inject a fake <see cref="IUiTimer"/> and
    /// drive ticks by hand; production uses the default <see cref="DispatcherTimerAdapter"/> above.</summary>
    internal MetricChannel(TimeSpan interval, int windowSize, Func<TSample> sample,
                           Func<TSample, double> historyValue, Action<TSample> onSample, Action onFailed, IUiTimer timer) {
        _history = new MetricHistory(windowSize);
        _sample = sample;
        _historyValue = historyValue;
        _onSample = onSample;
        _onFailed = onFailed;
        _timer = timer;
        _timer.Interval = interval;
        _timer.Tick += OnTick;
    }

    /// <summary>No-history variant: timer + sampling + fan-out only, for consumers that keep no rolling
    /// buffer (the shared <see cref="SystemMetricsService"/>, which fans each sample out to subscribers
    /// that own their own histories).</summary>
    public MetricChannel(TimeSpan interval, Func<TSample> sample, Action<TSample> onSample, Action onFailed)
        : this(interval, 0, sample, static _ => 0.0, onSample, onFailed) { }

    /// <summary>No-history test seam (see the windowed overload): takes the timer explicitly.</summary>
    internal MetricChannel(TimeSpan interval, Func<TSample> sample, Action<TSample> onSample, Action onFailed, IUiTimer timer)
        : this(interval, 0, sample, static _ => 0.0, onSample, onFailed, timer) { }

    /// <summary>The rolling history, oldest-first, carrying how much of itself is real. Live state; valid
    /// only for synchronous reads on the UI thread (the next tick mutates it in place).</summary>
    public MetricHistory History => _history;

    /// <summary>Starts (or resumes) periodic sampling. Drives the shell's Live pill.</summary>
    public void Start() => _timer.Start();

    /// <summary>Pauses periodic sampling. Drives the shell's Live pill; <see cref="SampleNow"/> still
    /// works while stopped.</summary>
    public void Stop() => _timer.Stop();

    /// <summary>Samples once immediately whether or not the timer is running — for Refresh while paused.</summary>
    public void SampleNow() => Tick();

    /// <summary>Retimes the sampling cadence (the Settings refresh-interval control). Takes effect on the
    /// next tick; a running timer keeps running at the new interval, a stopped one stays stopped.</summary>
    public void SetInterval(TimeSpan interval) => _timer.Interval = interval;

    private void OnTick(object? sender, EventArgs e) => Tick();

    private void Tick() {
        TSample value;
        try {
            value = _sample();
        } catch (Exception e) {
            // Counter unavailable: show the placeholder and stop polling rather than throwing every tick.
            Log.Warn("MetricChannel sampler failed; stopping channel", e);
            _onFailed();
            _timer.Stop();
            return;
        }

        _history.Push(_historyValue(value));
        _onSample(value);
    }

    /// <summary>Stops the timer and unsubscribes the tick handler. Safe to call more than once.</summary>
    public void Dispose() {
        _timer.Stop();
        _timer.Tick -= OnTick;
        GC.SuppressFinalize(this);
    }
}

/// <summary>Non-generic convenience for plain-<c>double</c> metrics (CPU/GPU): the window projection is
/// the identity.</summary>
public sealed class MetricChannel : MetricChannel<double> {
    public MetricChannel(TimeSpan interval, int windowSize, Func<double> sample,
                         Action<double> onSample, Action onFailed)
        : base(interval, windowSize, sample, static v => v, onSample, onFailed) { }

    /// <summary>Test seam: takes the timer explicitly (see <see cref="MetricChannel{TSample}"/>).</summary>
    internal MetricChannel(TimeSpan interval, int windowSize, Func<double> sample,
                           Action<double> onSample, Action onFailed, IUiTimer timer)
        : base(interval, windowSize, sample, static v => v, onSample, onFailed, timer) { }
}
