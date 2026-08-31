using DashDetective.Services.Network;
using DashDetective.Services.SystemMetrics;
using DashDetective.Tests.Fakes;
using System;
using System.Collections.Generic;
using Xunit;

namespace DashDetective.Tests.Services.SystemMetrics;

/// <summary>
/// Covers <see cref="ResourceAlertWatcher"/> through injected samplers, hardware stubs and fake timers:
/// the streak maths and its dependence on the live interval, "worst device wins and is named", the
/// inverted free-space rule, which breach is reported when several are live at once, and that nothing is
/// sampled until the setting is on and a threshold is actually set.
/// </summary>
public class ResourceAlertWatcherTests {
    // The metrics service builds its feeds in this order, so the captured timers line up by index.
    private const int Cpu = 0, Memory = 1;

    private sealed class FakeSamplers {
        public double Cpu = 50;
        public MemorySample Memory = new(10, 0, 0, 0, 0);

        public MetricSamplers Bundle() => new(
            () => Cpu, () => Memory, () => new NetworkSample(0, 0), () => "TestNIC");
    }

    private sealed class Harness {
        public required FakeSamplers Samplers { get; init; }
        public required SystemMetricsService Metrics { get; init; }
        public required List<FakeUiTimer> FeedTimers { get; init; }
        public required FakeUiTimer DeviceTimer { get; init; }
        public required FakeUiTimer SpaceTimer { get; init; }
        public required ResourceAlertWatcher Watcher { get; init; }
        public required List<ResourceAlert?> Changes { get; init; }
    }

    private static Harness Create(
        ResourceAlertOptions? options = null,
        FakeGpuUsageSampler? gpu = null,
        FakeDiskThroughputSampler? disks = null,
        HardwareProviders? hardware = null,
        bool enabled = true) {
        var samplers = new FakeSamplers();
        var feedTimers = new List<FakeUiTimer>();
        var metrics = new SystemMetricsService(samplers.Bundle(), () => {
            var timer = new FakeUiTimer();
            feedTimers.Add(timer);
            return timer;
        });

        var deviceTimer = new FakeUiTimer();
        var spaceTimer = new FakeUiTimer();
        var watcher = new ResourceAlertWatcher(
            metrics, gpu ?? new FakeGpuUsageSampler(), disks ?? new FakeDiskThroughputSampler(),
            hardware ?? StubHardwareProviders.With(), deviceTimer, spaceTimer);

        var changes = new List<ResourceAlert?>();
        watcher.AlertChanged += alert => changes.Add(alert);

        // Thresholds before the switch: the Options setter clears every streak, so the other order would
        // discard whatever had already been counted.
        watcher.Options = options ?? ResourceAlertOptions.Defaults;
        watcher.Enabled = enabled;

        return new Harness {
            Samplers = samplers, Metrics = metrics, FeedTimers = feedTimers,
            DeviceTimer = deviceTimer, SpaceTimer = spaceTimer, Watcher = watcher, Changes = changes,
        };
    }

    // ----- Enabling and the cost of being off -----

    [Fact]
    public void Disabled_SubscribesNothing_SoNoFeedRuns() {
        var harness = Create(enabled: false);

        Assert.All(harness.FeedTimers, timer => Assert.False(timer.IsRunning));
        Assert.False(harness.DeviceTimer.IsRunning);
        Assert.False(harness.SpaceTimer.IsRunning);
    }

    [Fact]
    public void Enabled_StartsCpuAndMemory_AndStopsThemAgainWhenCleared() {
        var harness = Create();

        Assert.True(harness.FeedTimers[Cpu].IsRunning);
        Assert.True(harness.FeedTimers[Memory].IsRunning);

        harness.Watcher.Enabled = false;
        Assert.False(harness.FeedTimers[Cpu].IsRunning);
        Assert.False(harness.FeedTimers[Memory].IsRunning);
    }

    /// <summary>The device timer costs a hardware read per tick, so it must not run for thresholds that
    /// are switched off — which is what GPU and disk activity are by default.</summary>
    [Fact]
    public void DeviceTimer_StaysStopped_WhileGpuAndDiskAreOff() {
        var harness = Create();

        Assert.False(harness.DeviceTimer.IsRunning);
        Assert.True(harness.SpaceTimer.IsRunning);   // low-disk-space IS on by default
    }

    [Fact]
    public void DeviceTimer_RunsOnceAThresholdWantsIt() {
        var harness = Create(ResourceAlertOptions.Defaults with { GpuPercent = 90 });

        Assert.True(harness.DeviceTimer.IsRunning);
    }

    [Fact]
    public void SetLive_False_StopsTheTimersThisWatcherOwns() {
        var harness = Create(ResourceAlertOptions.Defaults with { GpuPercent = 90 });

        harness.Watcher.SetLive(false);
        Assert.False(harness.DeviceTimer.IsRunning);
        Assert.False(harness.SpaceTimer.IsRunning);

        harness.Watcher.SetLive(true);
        Assert.True(harness.DeviceTimer.IsRunning);
        Assert.True(harness.SpaceTimer.IsRunning);
    }

    // ----- Streaks -----

    [Fact]
    public void Cpu_RaisesAfterASustainedBreach_ThenClearsOnRecovery() {
        var harness = Create();

        harness.Samplers.Cpu = 95;
        for (var i = 0; i < 10; i++)
            harness.Metrics.RefreshAll();

        var alert = Assert.Single(harness.Changes);
        Assert.Equal(AlertMetric.Cpu, alert!.Metric);
        Assert.Equal("CPU", alert.DeviceName);
        Assert.Equal(90, alert.Threshold);

        harness.Samplers.Cpu = 50;
        harness.Metrics.RefreshAll();
        Assert.Equal(2, harness.Changes.Count);
        Assert.Null(harness.Changes[1]);
    }

    /// <summary>A breach that is not sustained is not an alert — one spike must not raise a banner.</summary>
    [Fact]
    public void Cpu_BreachShorterThanTheWindow_RaisesNothing() {
        var harness = Create();

        harness.Samplers.Cpu = 95;
        for (var i = 0; i < 9; i++)
            harness.Metrics.RefreshAll();

        Assert.Empty(harness.Changes);
    }

    [Fact]
    public void Cpu_BreachInterrupted_RestartsTheStreak() {
        var harness = Create();

        harness.Samplers.Cpu = 95;
        for (var i = 0; i < 9; i++)
            harness.Metrics.RefreshAll();

        harness.Samplers.Cpu = 10;      // one sample under the threshold
        harness.Metrics.RefreshAll();

        harness.Samplers.Cpu = 95;
        for (var i = 0; i < 9; i++)
            harness.Metrics.RefreshAll();

        Assert.Empty(harness.Changes);   // the first nine no longer count towards the second run
    }

    /// <summary>The whole reason the window is stored in seconds. At the 5 s interval, ten seconds is two
    /// samples — a fixed count of ten would have meant fifty seconds here.</summary>
    [Fact]
    public void SustainWindow_IsSeconds_NotSamples() {
        var harness = Create();
        harness.Metrics.SetInterval(TimeSpan.FromSeconds(5));

        harness.Samplers.Cpu = 95;
        harness.Metrics.RefreshAll();
        Assert.Empty(harness.Changes);

        harness.Metrics.RefreshAll();
        Assert.Single(harness.Changes);
    }

    [Fact]
    public void SustainWindow_RoundsUp_AtSubSecondIntervals() {
        var harness = Create(ResourceAlertOptions.Defaults with { SustainSeconds = 5 });
        harness.Metrics.SetInterval(TimeSpan.FromSeconds(0.5));

        harness.Samplers.Cpu = 95;
        for (var i = 0; i < 9; i++)
            harness.Metrics.RefreshAll();
        Assert.Empty(harness.Changes);

        harness.Metrics.RefreshAll();   // the tenth half-second sample completes five seconds
        Assert.Single(harness.Changes);
    }

    /// <summary>A threshold of zero is how a metric is switched off, so it must never raise however high
    /// the reading goes.</summary>
    [Fact]
    public void ThresholdOfZero_NeverRaises() {
        var harness = Create(ResourceAlertOptions.Defaults with { CpuPercent = 0, MemoryPercent = 0 });

        harness.Samplers.Cpu = 100;
        for (var i = 0; i < 30; i++)
            harness.Metrics.RefreshAll();

        Assert.Empty(harness.Changes);
    }

    /// <summary>A streak counted against the old threshold says nothing about a new one.</summary>
    [Fact]
    public void ChangingOptions_ClearsTheStreaksInFlight() {
        var harness = Create();

        harness.Samplers.Cpu = 95;
        for (var i = 0; i < 9; i++)
            harness.Metrics.RefreshAll();

        harness.Watcher.Options = ResourceAlertOptions.Defaults with { CpuPercent = 80 };

        harness.Metrics.RefreshAll();
        Assert.Empty(harness.Changes);   // one sample against the new threshold, not the tenth of nine
    }

    /// <summary>A banner must not outlive the setting that raised it.</summary>
    [Fact]
    public void Disabling_DuringABreach_ClearsTheAlert() {
        var harness = Create();

        harness.Samplers.Cpu = 95;
        for (var i = 0; i < 10; i++)
            harness.Metrics.RefreshAll();
        Assert.NotNull(harness.Watcher.Current);

        harness.Watcher.Enabled = false;
        Assert.Null(harness.Watcher.Current);
        Assert.Equal(2, harness.Changes.Count);
        Assert.Null(harness.Changes[1]);
    }

    // ----- Multi-device: the worst one wins, and it is named -----

    [Fact]
    public void Gpu_ReportsTheBusiestAdapter_ByName() {
        var gpu = new FakeGpuUsageSampler().Reporting("luid_a", 10).Reporting("luid_b", 99);
        var hardware = StubHardwareProviders.With(gpuAdapters: [
            new GpuAdapter("luid_a", "Radeon Graphics", false, 0),
            new GpuAdapter("luid_b", "GeForce RTX 3060", false, 0),
        ]);
        var harness = Create(
            ResourceAlertOptions.Defaults with { GpuPercent = 90, SustainSeconds = 2 },
            gpu: gpu, hardware: hardware);

        harness.DeviceTimer.RaiseTick();
        harness.DeviceTimer.RaiseTick();

        var alert = Assert.Single(harness.Changes);
        Assert.Equal(AlertMetric.Gpu, alert!.Metric);
        Assert.Equal("GeForce RTX 3060", alert.DeviceName);
        Assert.Equal(99, alert.Value);
    }

    /// <summary>An adapter that exists but reports nothing must not be read as zero — that would let a
    /// silent adapter mask a busy one, or look like a recovery.</summary>
    [Fact]
    public void Gpu_IgnoresAnAdapterThatReportsNothing() {
        var gpu = new FakeGpuUsageSampler().Silent("luid_a").Reporting("luid_b", 99);
        var harness = Create(
            ResourceAlertOptions.Defaults with { GpuPercent = 90, SustainSeconds = 2 }, gpu: gpu);

        harness.DeviceTimer.RaiseTick();
        harness.DeviceTimer.RaiseTick();

        Assert.Equal(99, Assert.Single(harness.Changes)!.Value);
    }

    [Fact]
    public void Gpu_SamplerThatThrows_RaisesNothing() {
        var harness = Create(
            ResourceAlertOptions.Defaults with { GpuPercent = 90, SustainSeconds = 2 },
            gpu: new FakeGpuUsageSampler().Throwing());

        harness.DeviceTimer.RaiseTick();
        harness.DeviceTimer.RaiseTick();

        Assert.Empty(harness.Changes);
    }

    [Fact]
    public void DiskActivity_ReportsTheBusiestDisk_ByName() {
        var disks = new FakeDiskThroughputSampler().Reporting(0, 20).Reporting(1, 97);
        var hardware = StubHardwareProviders.With(disks: [
            new PhysicalDiskInfo(0, "Boot NVMe", "NVMe SSD", 0, true),
            new PhysicalDiskInfo(1, "Archive HDD", "HDD", 0, true),
        ]);
        var harness = Create(
            ResourceAlertOptions.Defaults with { DiskActivePercent = 90, SustainSeconds = 2 },
            disks: disks, hardware: hardware);

        harness.DeviceTimer.RaiseTick();
        harness.DeviceTimer.RaiseTick();

        var alert = Assert.Single(harness.Changes);
        Assert.Equal(AlertMetric.DiskActivity, alert!.Metric);
        Assert.Equal("Archive HDD", alert.DeviceName);
    }

    /// <summary>Names are cosmetic and load in the background, so a disk with no name yet still has to be
    /// distinguishable from the one beside it.</summary>
    [Fact]
    public void DiskActivity_FallsBackToTheDiskNumber() {
        var harness = Create(
            ResourceAlertOptions.Defaults with { DiskActivePercent = 90, SustainSeconds = 2 },
            disks: new FakeDiskThroughputSampler().Reporting(3, 97));

        harness.DeviceTimer.RaiseTick();
        harness.DeviceTimer.RaiseTick();

        Assert.Equal("Disk 3", Assert.Single(harness.Changes)!.DeviceName);
    }

    // ----- Free space: the inverted one -----

    [Fact]
    public async System.Threading.Tasks.Task LowDiskSpace_ReportsTheFullestVolume_WithoutWaitingOutTheWindow() {
        var hardware = StubHardwareProviders.With(volumes: [
            new VolumeInfo(0, 'C', "System", "NTFS", 1000, 40),    // 4% free
            new VolumeInfo(1, 'D', "Data", "NTFS", 1000, 800),     // 80% free
        ]);
        var harness = Create(hardware: hardware);

        harness.SpaceTimer.RaiseTick();
        await System.Threading.Tasks.Task.Yield();

        var alert = Assert.Single(harness.Changes);
        Assert.Equal(AlertMetric.DiskSpace, alert!.Metric);
        Assert.Equal("C: (System)", alert.DeviceName);
        Assert.Equal(10, alert.Threshold);
    }

    [Fact]
    public async System.Threading.Tasks.Task LowDiskSpace_RoomyVolumesRaiseNothing() {
        var hardware = StubHardwareProviders.With(volumes: [
            new VolumeInfo(0, 'C', "System", "NTFS", 1000, 800),
        ]);
        var harness = Create(hardware: hardware);

        harness.SpaceTimer.RaiseTick();
        await System.Threading.Tasks.Task.Yield();

        Assert.Empty(harness.Changes);
    }

    /// <summary>A zero-sized volume has no meaningful percentage; dividing by it would report 0% free and
    /// raise a permanent alert for something like an empty card reader.</summary>
    [Fact]
    public async System.Threading.Tasks.Task LowDiskSpace_SkipsUnsizedVolumes() {
        var hardware = StubHardwareProviders.With(volumes: [
            new VolumeInfo(0, 'E', "", "", 0, 0),
        ]);
        var harness = Create(hardware: hardware);

        harness.SpaceTimer.RaiseTick();
        await System.Threading.Tasks.Task.Yield();

        Assert.Empty(harness.Changes);
    }

    /// <summary>Windows keeps unlettered Recovery and EFI partitions, and they sit near-full by design.
    /// Watching them would mean a banner that is always on, naming a disk nobody can free space on — which
    /// is exactly what running the app turned up.</summary>
    [Fact]
    public async System.Threading.Tasks.Task LowDiskSpace_IgnoresUnletteredSystemPartitions() {
        var hardware = StubHardwareProviders.With(volumes: [
            new VolumeInfo(0, null, "Recovery", "NTFS", 1000, 70),   // 7% free, and unreachable
            new VolumeInfo(0, 'C', "System", "NTFS", 1000, 800),
        ]);
        var harness = Create(hardware: hardware);

        harness.SpaceTimer.RaiseTick();
        await System.Threading.Tasks.Task.Yield();

        Assert.Empty(harness.Changes);
    }

    /// <summary>On Linux a volume has a mount point rather than a letter, and the message has to name it
    /// with whichever it carries.</summary>
    [Fact]
    public async System.Threading.Tasks.Task LowDiskSpace_NamesAVolumeByItsMountPoint() {
        var hardware = StubHardwareProviders.With(volumes: [
            new VolumeInfo(0, null, "", "ext4", 1000, 10, MountPoint: "/"),
        ]);
        var harness = Create(hardware: hardware);

        harness.SpaceTimer.RaiseTick();
        await System.Threading.Tasks.Task.Yield();

        Assert.Equal("/", Assert.Single(harness.Changes)!.DeviceName);
    }

    // ----- Choosing between breaches -----

    /// <summary>The banner must not swap out from under someone reading it, so a breach already reported
    /// keeps the message while it lasts.</summary>
    [Fact]
    public void AnActiveBreach_IsNotReplacedByASecondOne() {
        var harness = Create();

        harness.Samplers.Cpu = 95;
        for (var i = 0; i < 10; i++)
            harness.Metrics.RefreshAll();

        harness.Samplers.Memory = new MemorySample(99, 0, 0, 0, 0);
        for (var i = 0; i < 10; i++)
            harness.Metrics.RefreshAll();

        Assert.Equal(AlertMetric.Cpu, Assert.Single(harness.Changes)!.Metric);
    }

    /// <summary>Once the reported breach recovers, a still-breaching resource takes over rather than the
    /// banner going quiet while the machine is still in trouble.</summary>
    [Fact]
    public void WhenTheReportedBreachRecovers_AnotherLiveOneTakesOver() {
        var harness = Create();

        harness.Samplers.Cpu = 95;
        harness.Samplers.Memory = new MemorySample(99, 0, 0, 0, 0);
        for (var i = 0; i < 10; i++)
            harness.Metrics.RefreshAll();
        Assert.Equal(AlertMetric.Cpu, harness.Watcher.Current!.Metric);

        harness.Samplers.Cpu = 10;
        harness.Metrics.RefreshAll();

        Assert.Equal(AlertMetric.Memory, harness.Watcher.Current!.Metric);
        Assert.Equal(2, harness.Changes.Count);   // one handover, not a clear and a raise
    }

    /// <summary>The readings change every sample; the banner text must not be rewritten every second.</summary>
    [Fact]
    public void ASustainedBreach_IsAnnouncedOnce() {
        var harness = Create();

        harness.Samplers.Cpu = 95;
        for (var i = 0; i < 30; i++)
            harness.Metrics.RefreshAll();

        Assert.Single(harness.Changes);
    }

    // ----- Lifetime -----

    [Fact]
    public void Dispose_StopsTheTimersAndDisposesTheSamplersItOwns() {
        var gpu = new FakeGpuUsageSampler();
        var disks = new FakeDiskThroughputSampler();
        var harness = Create(
            ResourceAlertOptions.Defaults with { GpuPercent = 90 }, gpu: gpu, disks: disks);

        harness.Watcher.Dispose();

        Assert.False(harness.DeviceTimer.IsRunning);
        Assert.False(harness.SpaceTimer.IsRunning);
        Assert.True(gpu.Disposed);
        Assert.True(disks.Disposed);
        Assert.All(harness.FeedTimers, timer => Assert.False(timer.IsRunning));
    }
}
