using DashDetective.Services.Network;
using DashDetective.Services.SystemMetrics;
using DashDetective.Services.Theming;
using DashDetective.Shared;
using DashDetective.Tabs.Performance;
using DashDetective.Tests.Fakes;
using System.Linq;
using Xunit;

namespace DashDetective.Tests.Tabs.Performance;

/// <summary>Pins that Refresh updates the CPU, memory and network rows once while the Live pill is off, leaving the page unsubscribed.</summary>
public class PerformanceRefreshWhilePausedTests {
    [Fact]
    public void Refresh_WhilePaused_UpdatesTheSharedMetricsOnce() {
        double cpu = 10;
        var memory = new MemorySample(20, 1UL << 30, 8UL << 30, 0, 0);
        var network = new NetworkSample(1, 0);
        var metrics = TestMetrics.WithTimers(
            out var timers, cpu: () => cpu, memory: () => memory, network: () => network);
        var page = new PerformanceViewModel(
            metrics, StubHardwareProviders.With(), () => new FakeGpuUsageSampler(),
            throughputSampler: new FakeDiskThroughputSampler());
        page.SetActive(true);
        // What the shell does for the Live pill: the service pauses, then each page.
        metrics.Pause();
        page.SetLive(false);

        cpu = 64;
        memory = new MemorySample(75, 6UL << 30, 8UL << 30, 0, 0);
        network = new NetworkSample(50, 0);
        page.Refresh();

        Assert.Equal("64", Row(page, ChartSeries.Cpu).ValueText);
        Assert.Equal("75", Row(page, ChartSeries.Memory).ValueText);
        Assert.Equal(DataRateFormatter.SplitPair(50, 0).First, Row(page, ChartSeries.NetDown).ValueText);
        Assert.All(timers, t => Assert.False(t.IsRunning));

        // Still detached, so a Resume does not restart a feed for this page.
        metrics.Resume();
        Assert.All(timers, t => Assert.False(t.IsRunning));
    }

    private static ResourceRow Row(PerformanceViewModel page, ChartSeries series) =>
        page.Resources.First(r => r.Series == series);
}
