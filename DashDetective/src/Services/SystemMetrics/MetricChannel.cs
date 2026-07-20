using Avalonia.Threading;
using DashDetective.Services.Diagnostics;
using System;

namespace DashDetective.Services.SystemMetrics;

/// <summary>
/// Reusable "sampler + <see cref="DispatcherTimer"/> + rolling <c>double[window]</c> history" unit,
/// replacing the per-metric timer/buffer pattern once copy-pasted across the view models. Each tick
/// samples once, appends a scalar projection to the window, and hands the full sample to <c>onSample</c>.
/// A sampler exception calls <c>onFailed</c> and permanently stops the timer (per-channel fault
/// isolation); <see cref="SampleNow"/> samples once regardless of timer state (for paused Refresh). The
/// two-history network case pushes its second buffer via <see cref="PushHistory"/>.
/// </summary>
/// <typeparam name="TSample">One sampler call's result — a <c>double</c> or a snapshot record.</typeparam>
public class MetricChannel<TSample> : IDisposable {
    private readonly DispatcherTimer _timer;
    private readonly Func<TSample> _sample;
    private readonly Func<TSample, double> _historyValue;
    private readonly Action<TSample> _onSample;
    private readonly Action _onFailed;
    private readonly double[] _history;

    /// <param name="historyValue">Projects the scalar pushed into the window (identity for a plain
    /// <c>double</c>; e.g. <c>s =&gt; s.LoadPercent</c> for a snapshot).</param>
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

/// <summary>Non-generic convenience for plain-<c>double</c> metrics (CPU/GPU): the window projection is
/// the identity.</summary>
public sealed class MetricChannel : MetricChannel<double> {
    public MetricChannel(TimeSpan interval, int windowSize, Func<double> sample,
                         Action<double> onSample, Action onFailed)
        : base(interval, windowSize, sample, static v => v, onSample, onFailed) { }
}
