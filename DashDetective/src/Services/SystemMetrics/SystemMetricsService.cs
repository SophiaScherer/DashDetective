using DashDetective.Services.Network;
using System;
using System.Collections.Generic;

namespace DashDetective.Services.SystemMetrics;

/// <summary>
/// Single owner of the live system samplers (CPU, Memory, GPU, Storage, Network). Each metric is sampled
/// once per 1 Hz tick and the value fanned out to every subscriber, so pages share one sampler instead of
/// each owning their own — this removes the duplicate PDH GPU/disk queries the Dashboard and Performance
/// tabs used to run in parallel.
///
/// Subscription-based and ref-counted: a metric's channel runs only while it has at least one subscriber.
/// <see cref="Pause"/>/<see cref="Resume"/> back the shell's Live pill, and <see cref="RefreshAll"/> backs
/// the toolbar Refresh (a one-shot sample even while paused). Per-metric fault isolation is preserved: one
/// sampler failing stops only that metric's channel and notifies its subscribers, never the others.
/// </summary>
public sealed class SystemMetricsService : IDisposable {
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(1);

    private readonly CpuUsageSampler _cpuSampler = new();
    private readonly MemoryUsageSampler _memorySampler = new();
    private readonly GpuUsageSampler _gpuSampler = new();
    private readonly StorageUsageSampler _storageSampler = new();
    private readonly NetworkUsageSampler _networkSampler = new();

    private readonly MetricFeed<double> _cpu;
    private readonly MetricFeed<MemorySample> _memory;
    private readonly MetricFeed<double> _gpu;
    private readonly MetricFeed<StorageSample> _storage;
    private readonly MetricFeed<NetworkSample> _network;
    private readonly MetricFeed[] _feeds;

    public SystemMetricsService() {
        _cpu = new MetricFeed<double>(Interval, () => _cpuSampler.Sample());
        _memory = new MetricFeed<MemorySample>(Interval, () => _memorySampler.Sample());
        _gpu = new MetricFeed<double>(Interval, () => _gpuSampler.Sample());
        _storage = new MetricFeed<StorageSample>(Interval, () => _storageSampler.Sample());
        _network = new MetricFeed<NetworkSample>(Interval, () => _networkSampler.Sample());
        _feeds = new MetricFeed[] { _cpu, _memory, _gpu, _storage, _network };
    }

    /// <summary>Friendly name of the sampled network adapter, for the throughput caption.</summary>
    public string NetworkAdapterName => _networkSampler.AdapterName;

    /// <summary>Subscribes to CPU utilisation (0–100). Returns a token; dispose it to unsubscribe.</summary>
    public IDisposable SubscribeCpu(Action<double> onSample, Action onFailed) => _cpu.Subscribe(onSample, onFailed);

    /// <summary>Subscribes to physical-memory snapshots. Returns a token; dispose it to unsubscribe.</summary>
    public IDisposable SubscribeMemory(Action<MemorySample> onSample, Action onFailed) => _memory.Subscribe(onSample, onFailed);

    /// <summary>Subscribes to GPU utilisation (0–100). Returns a token; dispose it to unsubscribe.</summary>
    public IDisposable SubscribeGpu(Action<double> onSample, Action onFailed) => _gpu.Subscribe(onSample, onFailed);

    /// <summary>Subscribes to physical-disk snapshots. Returns a token; dispose it to unsubscribe.</summary>
    public IDisposable SubscribeStorage(Action<StorageSample> onSample, Action onFailed) => _storage.Subscribe(onSample, onFailed);

    /// <summary>Subscribes to network throughput snapshots. Returns a token; dispose it to unsubscribe.</summary>
    public IDisposable SubscribeNetwork(Action<NetworkSample> onSample, Action onFailed) => _network.Subscribe(onSample, onFailed);

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

    /// <summary>Stops all channels and disposes the samplers that own PDH query handles (GPU, Storage).</summary>
    public void Dispose() {
        foreach (var feed in _feeds)
            feed.Dispose();
        _gpuSampler.Dispose();
        _storageSampler.Dispose();
    }

    /// <summary>Non-generic base so the service can iterate its feeds uniformly.</summary>
    private abstract class MetricFeed {
        public abstract void Pause();
        public abstract void Resume();
        public abstract void SampleNow();
        public abstract void Dispose();
    }

    /// <summary>
    /// One metric: a no-history <see cref="MetricChannel{TSample}"/> plus its subscriber list. Caches the
    /// latest sample and replays it to a new subscriber on subscribe, so a page seeds with real data on its
    /// first frame (as the old per-page constructors did). Ref-counted: the channel runs only while at
    /// least one subscriber is attached and sampling isn't paused.
    /// </summary>
    private sealed class MetricFeed<TSample> : MetricFeed {
        private readonly MetricChannel<TSample> _channel;
        private readonly List<Action<TSample>> _onSample = new();
        private readonly List<Action> _onFailed = new();
        private TSample _latest = default!;
        private bool _hasLatest;
        private bool _paused;

        public MetricFeed(TimeSpan interval, Func<TSample> sample) {
            _channel = new MetricChannel<TSample>(interval, sample, OnSample, OnFailed);

            // Prime the cache once so the first subscriber seeds with a real value (the samplers already
            // seeded their own baselines in their constructors). Never throws in practice.
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
