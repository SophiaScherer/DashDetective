using DashDetective.Services.SystemMetrics;
using DashDetective.Services.Theming;
using DashDetective.Tabs.Performance;
using DashDetective.Tests.Fakes;
using System;
using System.Linq;
using Xunit;

namespace DashDetective.Tests.Tabs.Performance;

/// <summary>Pins that a failed CPU sampler blanks the tiles riding its tick along with Utilization, rather than leaving a stale count.</summary>
public class PerformanceFailurePlaceholderTests {
    [Fact]
    public void CpuSamplerFailure_BlanksTheCountAndUptimeTiles() {
        var metrics = TestMetrics.WithTimers(
            out var timers, cpu: () => throw new InvalidOperationException());
        var page = new PerformanceViewModel(
            metrics, StubHardwareProviders.With(), () => new FakeGpuUsageSampler());
        page.SetActive(true);

        timers[TestMetrics.CpuTimer].RaiseTick();

        var stats = page.Resources.First(row => row.Series == ChartSeries.Cpu).Stats;
        foreach (var label in new[] { "Utilization", "Processes", "Threads", "Handles", "Up time" })
            Assert.Equal("—", stats.Single(tile => tile.Label == label).Value);
    }
}
