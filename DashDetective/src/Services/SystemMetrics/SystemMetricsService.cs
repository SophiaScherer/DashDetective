using DashDetective.Services.Network;
using DashDetective.Services.Threading;
using System;
using System.Collections.Generic;

namespace DashDetective.Services.SystemMetrics;

/// <summary>
/// Single owner of the shared live samplers (CPU, Memory, Network). Each metric is sampled once
/// per 1 Hz tick and fanned out to every subscriber, so pages share one sampler. Per-GPU and per-disk
/// readings are page-local instead (the Dashboard, Performance and Storage tabs own their own samplers),
/// since those are multi-instance — a shared aggregate feed would report an average across every device
/// under a label naming one of them. Subscriptions are ref-counted
/// (a channel runs only while it has subscribers); <see cref="Pause"/>/<see cref="Resume"/> back the Live
/// pill, <see cref="RefreshAll"/> backs Refresh, and per-metric fault isolation is kept.
/// </summary>
public sealed class SystemMetricsService : IDisposable {
    private static readonly TimeSpan DefaultInterval = TimeSpan.FromSeconds(1);

    /// <summary>Utilisation level (%) at or above which a metric counts as breaching, and the number of
    /// consecutive breaching samples that raises a resource alert (10 s at the default 1 Hz cadence).</summary>
    private const double AlertThresholdPercent = 90;
    private const int AlertConsecutiveSamples = 10;

    private readonly Func<string> _adapterName;
    private readonly IDisposable? _cpuDisposable;

    private readonly MetricFeed<double> _cpu;
    private readonly MetricFeed<MemorySample> _memory;
    private readonly MetricFeed<NetworkSample> _network;
    private readonly MetricFeed[] _feeds;

    // Internal resource-alert watcher: consecutive-breach streaks per metric and the combined state.
    private readonly IDisposable _cpuAlertSub;
    private readonly IDisposable _memoryAlertSub;
    private int _cpuBreachStreak;
    private int _memoryBreachStreak;
    private bool _alertActive;

    public SystemMetricsService()
        : this(CreateSystemSamplers()) { }

    // Unpacks the real sampler set, whose CPU sampler owns a native handle that must be disposed.
    private SystemMetricsService(SystemSamplers real)
        : this(real.Bundle, static () => new DispatcherTimerAdapter(), real.CpuDisposable) { }

    /// <summary>Test seam: injects the sampler delegates and the timer factory so ref-counting, fault
    /// isolation and the alert watcher can be exercised with fakes headlessly (see <see cref="IUiTimer"/>).
    /// The public parameterless ctor builds the real samplers and delegates here, so production is
    /// unchanged.</summary>
    internal SystemMetricsService(MetricSamplers samplers, Func<IUiTimer> timerFactory,
                                  IDisposable? cpuDisposable = null) {
        _adapterName = samplers.AdapterName;
        _cpuDisposable = cpuDisposable;

        _cpu = new MetricFeed<double>(DefaultInterval, samplers.Cpu, timerFactory);
        _memory = new MetricFeed<MemorySample>(DefaultInterval, samplers.Memory, timerFactory);
        _network = new MetricFeed<NetworkSample>(DefaultInterval, samplers.Network, timerFactory);
        _feeds = new MetricFeed[] { _cpu, _memory, _network };

        // Watch CPU + memory for a sustained breach. Subscribing keeps these two channels running, which
        // the always-on Dashboard already does; Pause still halts them (the Live pill), holding the streaks.
        _cpuAlertSub = _cpu.Subscribe(OnCpuAlertSample, static () => { });
        _memoryAlertSub = _memory.Subscribe(OnMemoryAlertSample, static () => { });
    }

    // Builds the three shared real samplers, each wrapped in a Sample() delegate; the CPU instance is also
    // returned as a disposable (it owns a native query handle disposed in Dispose). GPU and disk sampling are
    // page-local (per adapter / per disk), so no shared sampler for either lives here.
    private static SystemSamplers CreateSystemSamplers() {
        var cpu = new CpuUsageSampler();
        var memory = new MemoryUsageSampler();
        var network = new NetworkUsageSampler();

        var bundle = new MetricSamplers(
            () => cpu.Sample(), () => memory.Sample(),
            () => network.Sample(), () => network.AdapterName);
        return new SystemSamplers(bundle, cpu);
    }

    // Carries the real sampler bundle plus the native-handle owner that needs disposing.
    private readonly record struct SystemSamplers(MetricSamplers Bundle, IDisposable CpuDisposable);

    /// <summary>Raised when the resource-alert state flips: <c>true</c> once CPU or memory has stayed at or
    /// above the threshold for <see cref="AlertConsecutiveSamples"/> samples, <c>false</c> when both recover.
    /// The shell surfaces this as an inline banner (gated by the user's "Resource alerts" setting).</summary>
    public event Action<bool>? AlertActiveChanged;

    /// <summary>Whether a resource alert is currently active.</summary>
    public bool AlertActive => _alertActive;

    /// <summary>Friendly name of the sampled network adapter, for the throughput caption.</summary>
    public string NetworkAdapterName => _adapterName();

    /// <summary>Subscribes to CPU utilisation (0–100). Returns a token; dispose it to unsubscribe.</summary>
    public IDisposable SubscribeCpu(Action<double> onSample, Action onFailed) => _cpu.Subscribe(onSample, onFailed);

    /// <summary>Subscribes to physical-memory snapshots. Returns a token; dispose it to unsubscribe.</summary>
    public IDisposable SubscribeMemory(Action<MemorySample> onSample, Action onFailed) => _memory.Subscribe(onSample, onFailed);

    /// <summary>Subscribes to network throughput snapshots. Returns a token; dispose it to unsubscribe.</summary>
    public IDisposable SubscribeNetwork(Action<NetworkSample> onSample, Action onFailed) => _network.Subscribe(onSample, onFailed);

    /// <summary>The cadence the metric channels are running at — the Settings refresh interval. Pages that
    /// drive their own per-device samplers read this so their charts advance in step with the shared ones,
    /// and size their time-window captions from it.</summary>
    public TimeSpan Interval { get; private set; } = DefaultInterval;

    /// <summary>Raised when <see cref="SetInterval"/> changes the cadence, so a page can retime its own
    /// samplers. Not raised when the interval is unchanged.</summary>
    public event Action<TimeSpan>? IntervalChanged;

    /// <summary>
    /// Retimes the metric channels to the Settings refresh interval (0.5 / 1 / 2 / 5 s) and announces the
    /// change so page-local per-device samplers follow: a chart advancing at 1 Hz beside one advancing at
    /// 5 s covers a different span of time while being drawn the same width. The genuinely coarse timers
    /// stay coarse and do NOT follow (the Dashboard's 30 s uptime tick, the Network tab's 5 s adapter /
    /// 2.5 s connections / 2 s ping timers) — none of them feeds a chart. Applies even while paused, so a
    /// later Resume runs at the new cadence.
    /// </summary>
    public void SetInterval(TimeSpan interval) {
        if (interval == Interval)
            return;

        Interval = interval;
        foreach (var feed in _feeds)
            feed.SetInterval(interval);
        IntervalChanged?.Invoke(interval);
    }

    /// <summary>Updates the CPU breach streak and re-evaluates the alert state.</summary>
    private void OnCpuAlertSample(double cpuPercent) {
        _cpuBreachStreak = cpuPercent >= AlertThresholdPercent ? _cpuBreachStreak + 1 : 0;
        EvaluateAlert();
    }

    /// <summary>Updates the memory breach streak and re-evaluates the alert state.</summary>
    private void OnMemoryAlertSample(MemorySample sample) {
        _memoryBreachStreak = sample.LoadPercent >= AlertThresholdPercent ? _memoryBreachStreak + 1 : 0;
        EvaluateAlert();
    }

    /// <summary>An alert is active while either metric's streak has reached the consecutive-sample count;
    /// raises <see cref="AlertActiveChanged"/> only on a transition.</summary>
    private void EvaluateAlert() {
        var active = _cpuBreachStreak >= AlertConsecutiveSamples || _memoryBreachStreak >= AlertConsecutiveSamples;
        if (active == _alertActive)
            return;
        _alertActive = active;
        AlertActiveChanged?.Invoke(active);
    }

    /// <summary>Pauses all metric sampling (shell Live pill off). Refresh still works while paused.</summary>
    public void Pause() {
        foreach (var feed in _feeds)
            feed.Pause();
    }

    /// <summary>Resumes all metric sampling that has subscribers (shell Live pill on).</summary>
    public void Resume() {
        foreach (var feed in _feeds)
            feed.Resume();
    }

    /// <summary>Samples every subscribed metric once immediately and fans the results out — the toolbar
    /// Refresh, which must update once even while paused.</summary>
    public void RefreshAll() {
        foreach (var feed in _feeds)
            feed.SampleNow();
    }

    /// <summary>Stops all channels and disposes the sampler that owns a native query handle (CPU).</summary>
    public void Dispose() {
        _cpuAlertSub.Dispose();
        _memoryAlertSub.Dispose();
        foreach (var feed in _feeds)
            feed.Dispose();
        _cpuDisposable?.Dispose();
    }

    /// <summary>Non-generic base so the service can iterate its feeds uniformly.</summary>
    private abstract class MetricFeed {
        public abstract void Pause();
        public abstract void Resume();
        public abstract void SampleNow();
        public abstract void SetInterval(TimeSpan interval);
        public abstract void Dispose();
    }

    /// <summary>One metric: a no-history <see cref="MetricChannel{TSample}"/> plus its subscriber list.
    /// Caches the latest sample and replays it on subscribe (so a page seeds with real data at once), and
    /// runs the channel only while it has subscribers and isn't paused.</summary>
    private sealed class MetricFeed<TSample> : MetricFeed {
        private readonly MetricChannel<TSample> _channel;
        private readonly List<Action<TSample>> _onSample = new();
        private readonly List<Action> _onFailed = new();
        private TSample _latest = default!;
        private bool _hasLatest;
        private bool _paused;

        public MetricFeed(TimeSpan interval, Func<TSample> sample, Func<IUiTimer> timerFactory) {
            _channel = new MetricChannel<TSample>(interval, sample, OnSample, OnFailed, timerFactory());

            // Prime the cache once so the first subscriber seeds with a real value.
            try {
                _latest = sample();
                _hasLatest = true;
            } catch {
                _hasLatest = false;
            }
        }

        public IDisposable Subscribe(Action<TSample> onSample, Action onFailed) {
            _onSample.Add(onSample);
            _onFailed.Add(onFailed);

            // Seed the new subscriber immediately with the latest cached sample.
            if (_hasLatest)
                onSample(_latest);

            if (_onSample.Count == 1 && !_paused)
                _channel.Start();

            return new Subscription(this, onSample, onFailed);
        }

        private void Unsubscribe(Action<TSample> onSample, Action onFailed) {
            _onSample.Remove(onSample);
            _onFailed.Remove(onFailed);
            if (_onSample.Count == 0)
                _channel.Stop();
        }

        private void OnSample(TSample sample) {
            _latest = sample;
            _hasLatest = true;
            foreach (var callback in _onSample)
                callback(sample);
        }

        private void OnFailed() {
            foreach (var callback in _onFailed)
                callback();
        }

        public override void Pause() {
            _paused = true;
            _channel.Stop();
        }

        public override void Resume() {
            _paused = false;
            if (_onSample.Count > 0)
                _channel.Start();
        }

        public override void SampleNow() {
            if (_onSample.Count > 0)
                _channel.SampleNow();
        }

        public override void SetInterval(TimeSpan interval) => _channel.SetInterval(interval);

        public override void Dispose() => _channel.Dispose();

        /// <summary>Removes a subscriber's callbacks when disposed. Idempotent.</summary>
        private sealed class Subscription(MetricFeed<TSample> feed, Action<TSample> onSample, Action onFailed) : IDisposable {
            private bool _disposed;

            public void Dispose() {
                if (_disposed)
                    return;
                _disposed = true;
                feed.Unsubscribe(onSample, onFailed);
            }
        }
    }
}

/// <summary>
/// The three shared per-metric sampler delegates plus the network adapter-name accessor that
/// <see cref="SystemMetricsService"/> fans out. The parameterless ctor wraps the real hardware samplers;
/// the test seam injects fakes so the service's behaviour can be driven deterministically.
/// </summary>
internal sealed record MetricSamplers(
    Func<double> Cpu, Func<MemorySample> Memory,
    Func<NetworkSample> Network, Func<string> AdapterName);
