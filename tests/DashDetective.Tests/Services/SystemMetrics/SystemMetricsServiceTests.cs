using DashDetective.Services.Network;
using DashDetective.Services.SystemMetrics;
using DashDetective.Tests.Fakes;
using System;
using System.Collections.Generic;
using Xunit;

namespace DashDetective.Tests.Services.SystemMetrics;

/// <summary>Covers <see cref="SystemMetricsService"/> through the injected sampler bundle + fake timer
/// factory: ref-counted start/stop, Pause/Resume, seed-on-subscribe and per-metric fault isolation. The
/// resource-alert behaviour this class used to own now lives in <c>ResourceAlertWatcherTests</c>.</summary>
public class SystemMetricsServiceTests {
    // Feeds are constructed in this order, so the captured timers line up by index.
    private const int Cpu = 0, Memory = 1, Network = 2;

    /// <summary>Mutable fake sampler values; the bundle closes over these so a test can change a reading
    /// between refreshes (or make the network sampler throw).</summary>
    private sealed class FakeSamplers {
        public double Cpu = 50;
        public MemorySample Memory = new(10, 0, 0, 0, 0);
        public NetworkSample Network = new(0, 0);
        public bool NetworkThrows;
        public string AdapterName = "TestNIC";

        public MetricSamplers Bundle() => new(
            () => Cpu, () => Memory,
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
        var networkTimer = timers[Network];
        Assert.Equal(0, networkTimer.StartCount);   // Network has no auto-subscriber

        var token = service.SubscribeNetwork(_ => { }, () => { });
        Assert.True(networkTimer.IsRunning);
        Assert.Equal(1, networkTimer.StartCount);

        token.Dispose();
        Assert.False(networkTimer.IsRunning);
        Assert.True(networkTimer.StopCount >= 1);
    }

    [Fact]
    public void Construction_SubscribesNothing_SoNoFeedRuns() {
        var (_, timers) = Create(new FakeSamplers());
        Assert.All(timers, t => Assert.False(t.IsRunning));
    }

    [Fact]
    public void PauseThenResume_StopsAllThenRestartsOnlySubscribed() {
        var (service, timers) = Create(new FakeSamplers());
        service.SubscribeCpu(_ => { }, () => { });
        service.SubscribeMemory(_ => { }, () => { });
        var token = service.SubscribeNetwork(_ => { }, () => { });   // Network now has a subscriber

        service.Pause();
        Assert.All(timers, t => Assert.False(t.IsRunning));

        service.Resume();
        Assert.True(timers[Cpu].IsRunning);
        Assert.True(timers[Memory].IsRunning);
        Assert.True(timers[Network].IsRunning);

        // With its only subscriber gone, Network stays stopped across a Pause/Resume while the two that
        // still have one come back.
        token.Dispose();
        service.Pause();
        service.Resume();
        Assert.True(timers[Cpu].IsRunning);
        Assert.True(timers[Memory].IsRunning);
        Assert.False(timers[Network].IsRunning);
    }

    [Fact]
    public void RefreshAll_FansLatestSampleToSubscribers() {
        var fakes = new FakeSamplers();
        var (service, _) = Create(fakes);
        NetworkSample? received = null;
        service.SubscribeNetwork(v => received = v, () => { });

        fakes.Network = new NetworkSample(77, 0);
        service.RefreshAll();

        Assert.Equal(77, received?.DownMbps);
    }

    [Fact]
    public void Subscribe_SeedsWithCachedLatest() {
        var fakes = new FakeSamplers { Network = new NetworkSample(42, 0) };   // primed into the cache
        var (service, _) = Create(fakes);

        NetworkSample? seeded = null;
        service.SubscribeNetwork(v => seeded = v, () => { });

        Assert.Equal(42, seeded?.DownMbps);
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

}
