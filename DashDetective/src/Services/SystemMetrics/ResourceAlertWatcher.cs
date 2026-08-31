using DashDetective.Services.Diagnostics;
using DashDetective.Services.Threading;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace DashDetective.Services.SystemMetrics;

/// <summary>
/// Watches the machine's resources against the user's thresholds and reports the breach the shell shows
/// as a banner. Lives outside <see cref="SystemMetricsService"/>, which stays a pure fan-out of the three
/// shared feeds: GPU and disk readings are deliberately not shared feeds there, because an aggregate
/// across several devices would report an average under a label naming one of them.
///
/// This watcher can read them anyway without breaking that rule, because it never aggregates — it takes
/// the <b>worst</b> device and names it. It owns its own <see cref="IGpuUsageSampler"/> and
/// <see cref="IPhysicalDiskThroughputSampler"/>, which their contracts require (both are stateful and
/// report the interval since the caller's own previous call, so an instance may not be shared).
///
/// Everything is attached only while <see cref="Enabled"/>, so switching alerts off costs nothing: the
/// CPU/memory subscriptions are what would otherwise hold those feeds open with no page on screen, and
/// the device timers are what would otherwise poll hardware nobody is looking at.
/// </summary>
internal sealed class ResourceAlertWatcher : IDisposable {
    /// <summary>Free space changes over minutes, not seconds, so it is polled far more coarsely than the
    /// usage metrics — and each poll enumerates volumes, which is not free.</summary>
    private static readonly TimeSpan SpaceInterval = TimeSpan.FromSeconds(60);

    private readonly SystemMetricsService _metrics;
    private readonly IGpuUsageSampler _gpu;
    private readonly IPhysicalDiskThroughputSampler _disks;
    private readonly HardwareProviders _hardware;

    private readonly MetricSubscriptions _feeds;
    private readonly IUiTimer _deviceTimer;
    private readonly IUiTimer _spaceTimer;

    private readonly Dictionary<AlertMetric, MetricState> _states = new() {
        [AlertMetric.Cpu] = new MetricState(),
        [AlertMetric.Memory] = new MetricState(),
        [AlertMetric.Gpu] = new MetricState(),
        [AlertMetric.DiskActivity] = new MetricState(),
        [AlertMetric.DiskSpace] = new MetricState(),
    };

    // Friendly names, loaded once on the first attach. Cosmetic: until they arrive a device is named by
    // its number, which is still enough to tell two of them apart.
    private readonly Dictionary<string, string> _gpuNames = [];
    private readonly Dictionary<int, string> _diskNames = [];
    private bool _namesRequested;

    private ResourceAlertOptions _options = ResourceAlertOptions.Defaults;
    private ResourceAlert? _current;
    private bool _live = true;
    private bool _disposed;

    public ResourceAlertWatcher(SystemMetricsService metrics)
        : this(metrics, IGpuUsageSampler.ForCurrentPlatform(),
               IPhysicalDiskThroughputSampler.ForCurrentPlatform(),
               HardwareProviders.ForCurrentPlatform(),
               new DispatcherTimerAdapter(), new DispatcherTimerAdapter()) { }

    /// <summary>Test seam: injects the two samplers, the hardware readers and both timers, so streak
    /// maths and device naming can be driven headlessly (see <see cref="IUiTimer"/>).</summary>
    internal ResourceAlertWatcher(SystemMetricsService metrics, IGpuUsageSampler gpu,
                                  IPhysicalDiskThroughputSampler disks, HardwareProviders hardware,
                                  IUiTimer deviceTimer, IUiTimer spaceTimer) {
        _metrics = metrics;
        _gpu = gpu;
        _disks = disks;
        _hardware = hardware;

        _deviceTimer = deviceTimer;
        _deviceTimer.Interval = metrics.Interval;
        _deviceTimer.Tick += (_, _) => SampleDevices();

        _spaceTimer = spaceTimer;
        _spaceTimer.Interval = SpaceInterval;
        _spaceTimer.Tick += (_, _) => _ = SampleSpaceAsync();

        // Built detached: alerts are off by default, and subscribing would hold both feeds open however
        // little else is running.
        _feeds = new MetricSubscriptions(
            () => metrics.SubscribeCpu(OnCpuSample, static () => { }),
            () => metrics.SubscribeMemory(OnMemorySample, static () => { }));

        // The device timer draws no chart, but it must stay in step with the shared feeds: the sustain
        // window is expressed in seconds and converted against whatever the live cadence is.
        _metrics.IntervalChanged += OnIntervalChanged;
    }

    /// <summary>Raised when the reported breach changes — a new alert, a different resource, or recovery
    /// (<c>null</c>). Not raised for a fresh reading of a breach already being reported, so the banner
    /// text does not churn once per second.</summary>
    public event Action<ResourceAlert?>? AlertChanged;

    /// <summary>The breach currently being reported, or <c>null</c>.</summary>
    public ResourceAlert? Current => _current;

    /// <summary>Mirrors the user's "Resource alerts" setting. Switching it off clears any active alert,
    /// so a banner cannot outlive the setting that raised it.</summary>
    public bool Enabled {
        get => _feeds.IsAttached;
        set {
            if (value == _feeds.IsAttached)
                return;

            if (value) {
                _feeds.Attach();
                RequestDeviceNames();
            } else {
                _feeds.Detach();
                ClearStates();
            }

            ApplyTimers();
            Evaluate();
        }
    }

    /// <summary>The thresholds to watch against. Setting them clears every streak: a streak counted
    /// against the old threshold says nothing about the new one.</summary>
    public ResourceAlertOptions Options {
        get => _options;
        set {
            _options = value;
            ClearStates();
            ApplyTimers();
            Evaluate();
        }
    }

    /// <summary>Follows the toolbar's Live pill, like <c>ILiveSamplingPage</c>. The CPU and memory feeds
    /// are paused by the metrics service itself; this stops the two timers this watcher owns.</summary>
    public void SetLive(bool live) {
        _live = live;
        ApplyTimers();
    }

    /// <summary>How many consecutive breaching samples the sustain window works out to at the live
    /// cadence. Derived rather than stored, so retiming the feeds retimes the wait with them — a fixed
    /// sample count would silently mean 5 s at the 0.5 s interval and 50 s at the 5 s one.</summary>
    private int RequiredSamples {
        get {
            var seconds = _metrics.Interval.TotalSeconds;
            if (seconds <= 0)
                return 1;

            return Math.Max(1, (int)Math.Ceiling(_options.SustainSeconds / seconds));
        }
    }

    private void OnIntervalChanged(TimeSpan interval) => _deviceTimer.Interval = interval;

    /// <summary>Runs a timer only while it has something to watch — alerts on, sampling live, and at
    /// least one of the thresholds it serves actually set.</summary>
    private void ApplyTimers() {
        Run(_deviceTimer, _options.GpuPercent > 0 || _options.DiskActivePercent > 0);
        Run(_spaceTimer, _options.LowDiskFreePercent > 0);

        void Run(IUiTimer timer, bool wanted) {
            if (Enabled && _live && wanted)
                timer.Start();
            else
                timer.Stop();
        }
    }

    private void OnCpuSample(double percent) {
        RecordSustained(AlertMetric.Cpu, percent, "CPU", _options.CpuPercent);
        Evaluate();
    }

    private void OnMemorySample(MemorySample sample) {
        RecordSustained(AlertMetric.Memory, sample.LoadPercent, "Memory", _options.MemoryPercent);
        Evaluate();
    }

    /// <summary>Reads every GPU and every disk, and keeps the worst of each. Named rather than averaged:
    /// one adapter at 100% beside an idle one is not "50% GPU".</summary>
    private void SampleDevices() {
        SampleWorstGpu();
        SampleWorstDisk();
        Evaluate();
    }

    private void SampleWorstGpu() {
        if (_options.GpuPercent <= 0)
            return;

        double? worst = null;
        var device = "GPU";

        try {
            foreach (var (key, sample) in _gpu.SampleAdapters())
                if (sample.Overall is { } overall && (worst is null || overall > worst)) {
                    worst = overall;
                    device = _gpuNames.TryGetValue(key, out var name) ? name : "GPU";
                }
        } catch (Exception e) {
            // The sampler contract says it never throws, but a shut PDH query has; a dead reading must
            // not take the watcher down with it.
            Log.Warn("Could not sample GPU utilisation for the alert watcher", e);
            worst = null;
        }

        RecordSustained(AlertMetric.Gpu, worst, device, _options.GpuPercent);
    }

    private void SampleWorstDisk() {
        if (_options.DiskActivePercent <= 0)
            return;

        double? worst = null;
        var device = "Disk";

        try {
            foreach (var sample in _disks.Sample())
                if (worst is null || sample.ActivePercent > worst) {
                    worst = sample.ActivePercent;
                    device = DiskName(sample.DiskNumber);
                }
        } catch (Exception e) {
            // Same reason as the GPU arm above.
            Log.Warn("Could not sample disk activity for the alert watcher", e);
            worst = null;
        }

        RecordSustained(AlertMetric.DiskActivity, worst, device, _options.DiskActivePercent);
    }

    /// <summary>Keeps the volume with the least headroom. No streak: a full disk is not a spike that
    /// passes, so waiting out the sustain window would only delay the message.</summary>
    private async Task SampleSpaceAsync() {
        var threshold = _options.LowDiskFreePercent;
        if (threshold <= 0)
            return;

        double? worst = null;
        var device = "Disk";

        try {
            foreach (var volume in await _hardware.Volumes.GetAsync()) {
                if (volume.SizeBytes == 0)
                    continue;   // an unsized volume has no meaningful percentage

                // Only volumes the user can actually reach. The provider deliberately includes the
                // unlettered Recovery and EFI partitions, and those sit at ~95% full by design on every
                // Windows machine — watching them would mean a banner that is always on, naming a disk
                // nobody can free space on.
                if (!IsAddressable(volume))
                    continue;

                var free = volume.FreeBytes / (double)volume.SizeBytes * 100;
                if (worst is null || free < worst) {
                    worst = free;
                    device = VolumeName(volume);
                }
            }
        } catch (Exception e) {
            // A volume enumeration that failed leaves the previous verdict standing rather than clearing
            // it, so a transient failure cannot silently retract a low-space warning.
            Log.Warn("Could not read volume free space for the alert watcher", e);
            return;
        }

        var state = _states[AlertMetric.DiskSpace];
        state.Breaching = worst is { } headroom && headroom <= threshold;
        if (state.Breaching) {
            state.Value = worst!.Value;
            state.Device = device;
            state.Threshold = threshold;
        }

        Evaluate();
    }

    /// <summary>Advances (or clears) a metric's breach streak. A threshold of zero, a missing reading and
    /// a reading under the threshold all mean the same thing here: not breaching.</summary>
    private void RecordSustained(AlertMetric metric, double? value, string device, int threshold) {
        var state = _states[metric];

        if (threshold <= 0 || value is not { } reading || reading < threshold) {
            state.Streak = 0;
            state.Breaching = false;
            return;
        }

        state.Streak++;
        state.Value = reading;
        state.Device = device;
        state.Threshold = threshold;
        state.Breaching = state.Streak >= RequiredSamples;
    }

    /// <summary>
    /// Decides what to report. A breach already on screen keeps the banner while it lasts, so a second
    /// resource crossing its own threshold does not swap the message out from under someone reading it;
    /// otherwise the first breaching metric in <see cref="AlertMetric"/> order wins.
    /// </summary>
    private void Evaluate() {
        var metric = _current is { } current && _states[current.Metric].Breaching
            ? current.Metric
            : FirstBreaching();

        if (metric is null) {
            if (_current is null)
                return;

            _current = null;
            AlertChanged?.Invoke(null);
            return;
        }

        // Only an identity change is announced; a fresh reading of the same breach is not, or the banner
        // would be rewritten once per sample.
        if (_current?.Metric == metric)
            return;

        var state = _states[metric.Value];
        _current = new ResourceAlert(metric.Value, state.Device, state.Value, state.Threshold);
        AlertChanged?.Invoke(_current);
    }

    private AlertMetric? FirstBreaching() {
        foreach (var metric in Enum.GetValues<AlertMetric>())
            if (_states[metric].Breaching)
                return metric;

        return null;
    }

    private void ClearStates() {
        foreach (var state in _states.Values) {
            state.Streak = 0;
            state.Breaching = false;
        }
    }

    private string DiskName(int number) =>
        _diskNames.TryGetValue(number, out var name)
            ? name
            : string.Create(CultureInfo.InvariantCulture, $"Disk {number}");

    /// <summary>Whether a volume has somewhere the user could go to free space — a drive letter on
    /// Windows, a mount point on Linux. One with neither cannot be opened, so it cannot be acted on.</summary>
    private static bool IsAddressable(VolumeInfo volume) =>
        volume.DriveLetter is not null || !string.IsNullOrWhiteSpace(volume.MountPoint);

    /// <summary>A volume reads as the user knows it: its letter on Windows, its mount point on Linux, and
    /// its label where it has one. Only ever called for an addressable volume, so there is no unnamed
    /// case to fall back for.</summary>
    private static string VolumeName(VolumeInfo volume) {
        var id = volume.DriveLetter is { } letter
            ? string.Create(CultureInfo.InvariantCulture, $"{letter}:")
            : volume.MountPoint;

        return string.IsNullOrWhiteSpace(volume.Label) ? id : $"{id} ({volume.Label})";
    }

    /// <summary>Loads the friendly GPU and disk names once, in the background. Fire-and-forget on purpose:
    /// the names only improve a message, so nothing waits on them and a failure costs a nicer label.</summary>
    private void RequestDeviceNames() {
        if (_namesRequested)
            return;

        _namesRequested = true;
        _ = LoadDeviceNamesAsync();
    }

    private async Task LoadDeviceNamesAsync() {
        try {
            foreach (var adapter in await _hardware.GpuAdapters.GetAsync())
                if (!adapter.IsSoftware)
                    _gpuNames[adapter.LuidToken] = adapter.Name;

            foreach (var disk in await _hardware.Disks.GetAsync())
                _diskNames[disk.DeviceId] = disk.Model;
        } catch (Exception e) {
            // Names are cosmetic, so a failure costs a nicer label and nothing else.
            Log.Warn("Could not read device names for the alert watcher", e);
        }
    }

    public void Dispose() {
        if (_disposed)
            return;
        _disposed = true;

        _metrics.IntervalChanged -= OnIntervalChanged;
        _deviceTimer.Stop();
        _spaceTimer.Stop();
        _feeds.Dispose();
        _gpu.Dispose();
        _disks.Dispose();
    }

    /// <summary>One metric's running state: how long it has been over, and the reading and device that
    /// put it there.</summary>
    private sealed class MetricState {
        public int Streak;
        public bool Breaching;
        public double Value;
        public string Device = "";
        public int Threshold;
    }
}
