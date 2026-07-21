using DashDetective.Services.Network;
using DashDetective.Services.SystemMetrics;
using DashDetective.Tests.Fakes;
using System;
using System.Collections.Generic;
using Xunit;

namespace DashDetective.Tests.Services.SystemMetrics;

/// <summary>Covers <see cref="SystemMetricsService"/> through the injected sampler bundle + fake timer
/// factory: ref-counted start/stop, Pause/Resume, seed-on-subscribe, per-metric fault isolation, and
/// the sustained-breach alert watcher.</summary>
public class SystemMetricsServiceTests {
    // Feeds are constructed in this order, so the captured timers line up by index.
    private const int Cpu = 0, Memory = 1, Gpu = 2, Storage = 3, Network = 4;

    /// <summary>Mutable fake sampler values; the bundle closes over these so a test can change a reading
    /// between refreshes (or make the network sampler throw).</summary>
    private sealed class FakeSamplers {
        public double Cpu = 50;
        public MemorySample Memory = new(10, 0, 0, 0, 0);
        public double Gpu = 33;
        public StorageSample Storage = new(0, 0, 0, 0, 0);
        public NetworkSample Network = new(0, 0);
        public bool NetworkThrows;
        public string AdapterName = "TestNIC";

        public MetricSamplers Bundle() => new(
            () => Cpu, () => Memory, () => Gpu, () => Storage,
            () => NetworkThrows ? throw new InvalidOperationException("nic gone") : Network,
            () => AdapterName);
    }

    private static (SystemMetricsService Service, List<FakeUiTimer> Timers) Create(FakeSamplers fakes) {
        var timers = new List<FakeUiTimer>();
        var service = new SystemMetricsService(fakes.Bundle(), () => {
            var timer = new FakeUiTimer();
            timers.Add(timer);
            return timer;
        });
        return (service, timers);
    }

    [Fact]
    public void Subscribe_FirstStartsChannel_LastUnsubscribeStops() {
        var (service, timers) = Create(new FakeSamplers());
        var gpuTimer = timers[Gpu];
        Assert.Equal(0, gpuTimer.StartCount);   // GPU has no auto-subscriber

        var token = service.SubscribeGpu(_ => { }, () => { });
        Assert.True(gpuTimer.IsRunning);
        Assert.Equal(1, gpuTimer.StartCount);

        token.Dispose();
        Assert.False(gpuTimer.IsRunning);
        Assert.True(gpuTimer.StopCount >= 1);
    }

    [Fact]
    public void CpuAndMemory_AreAutoSubscribedForAlerts_AndStartAtConstruction() {
        var (_, timers) = Create(new FakeSamplers());
        Assert.True(timers[Cpu].IsRunning);
        Assert.True(timers[Memory].IsRunning);
        Assert.False(timers[Gpu].IsRunning);
    }

    [Fact]
    public void PauseThenResume_StopsAllThenRestartsOnlySubscribed() {
        var (service, timers) = Create(new FakeSamplers());
        service.SubscribeGpu(_ => { }, () => { });   // GPU now has a subscriber

        service.Pause();
        Assert.All(timers, t => Assert.False(t.IsRunning));

        service.Resume();
        Assert.True(timers[Cpu].IsRunning);       // alert subscriber
        Assert.True(timers[Memory].IsRunning);    // alert subscriber
        Assert.True(timers[Gpu].IsRunning);       // our subscriber
        Assert.False(timers[Storage].IsRunning);  // no subscriber
        Assert.False(timers[Network].IsRunning);
    }

    [Fact]
    public void RefreshAll_FansLatestSampleToSubscribers() {
        var fakes = new FakeSamplers();
        var (service, _) = Create(fakes);
        double? received = null;
        service.SubscribeGpu(v => received = v, () => { });

        fakes.Gpu = 77;
        service.RefreshAll();

        Assert.Equal(77, received);
    }

    [Fact]
    public void Subscribe_SeedsWithCachedLatest() {
        var fakes = new FakeSamplers { Gpu = 42 };   // primed into the cache at construction
        var (service, _) = Create(fakes);

        double? seeded = null;
        service.SubscribeGpu(v => seeded = v, () => { });

        Assert.Equal(42, seeded);
    }

    [Fact]
    public void OneSamplerFailure_DoesNotStopTheOthers() {
        var fakes = new FakeSamplers();
        var (service, timers) = Create(fakes);
        var networkFailed = false;
        var cpuSamples = 0;
        service.SubscribeNetwork(_ => { }, () => networkFailed = true);
        service.SubscribeCpu(_ => cpuSamples++, () => { });

        fakes.NetworkThrows = true;
        service.RefreshAll();

        Assert.True(networkFailed);               // the network channel's onFailed fired
        Assert.False(timers[Network].IsRunning);  // ...and only it stopped
        Assert.True(cpuSamples > 0);              // cpu still delivered
        Assert.True(timers[Cpu].IsRunning);       // ...and stays running
    }

    [Fact]
    public void Alert_RaisesTrueAfterSustainedCpuBreach_ThenFalseOnRecovery() {
        var fakes = new FakeSamplers();   // cpu starts at 50, below the threshold
        var (service, _) = Create(fakes);
        var transitions = new List<bool>();
        service.AlertActiveChanged += active => transitions.Add(active);

        fakes.Cpu = 95;
        for (var i = 0; i < 10; i++)
            service.RefreshAll();
        Assert.Equal(new[] { true }, transitions);   // fires once, on the 10th consecutive breach

        fakes.Cpu = 50;
        service.RefreshAll();
        Assert.Equal(new[] { true, false }, transitions);   // and once more on recovery
    }
}
