using DashDetective.Services.Network;
using DashDetective.Services.SystemMetrics;
using DashDetective.Shared;
using DashDetective.Tabs.Dashboard;
using DashDetective.Tests.Fakes;
using Xunit;

namespace DashDetective.Tests.Tabs.Dashboard;

/// <summary>Pins that Refresh updates CPU, memory and network once while the Live pill is off, leaving the page unsubscribed.</summary>
public class DashboardRefreshWhilePausedTests {
    [Fact]
    public void RefreshNow_WhilePaused_UpdatesTheSharedMetricsOnce() {
        double cpu = 10;
        var memory = new MemorySample(20, 1UL << 30, 8UL << 30, 0, 0);
        var network = new NetworkSample(1, 0);
        var metrics = TestMetrics.WithTimers(
            out var timers, cpu: () => cpu, memory: () => memory, network: () => network);
        var page = new DashboardViewModel(
            metrics, StubHardwareProviders.With(), () => new FakeGpuUsageSampler(), new FakeDiskThroughputSampler());
        page.SetActive(true);
        // What the shell does for the Live pill: the service pauses, then each page.
        metrics.Pause();
        page.SetLive(false);

        cpu = 64;
        memory = new MemorySample(75, 6UL << 30, 8UL << 30, 0, 0);
        network = new NetworkSample(50, 0);
        page.RefreshNow();

        Assert.Equal("64", page.CpuValueText);
        Assert.Equal("6.0", page.MemoryValueText);
        Assert.Equal(DataRateFormatter.SplitPair(50, 0).First, page.NetworkDownText);
        Assert.All(timers, t => Assert.False(t.IsRunning));

        // Still detached, so a Resume does not restart a feed for this page.
        metrics.Resume();
        Assert.All(timers, t => Assert.False(t.IsRunning));
    }
}
