using DashDetective.Services.Network;
using DashDetective.Services.SystemMetrics;
using DashDetective.Shared;
using DashDetective.Tabs.Dashboard;
using DashDetective.Tests.Fakes;
using System;
using System.Linq;
using Xunit;

namespace DashDetective.Tests.Tabs.Dashboard;

/// <summary>
/// Pins that a failed CPU, memory or network sampler leaves the Dashboard cards showing a bare placeholder:
/// the unit and the companion readout (memory ratio, network upload) go with the value rather than staying
/// live beside a dash.
/// </summary>
public class DashboardFailurePlaceholderTests {
    [Fact]
    public void SamplersThatFailAfterWorking_BlankTheUnitsAndCompanionReadouts() {
        var failing = false;
        var metrics = TestMetrics.WithTimers(
            out var timers,
            cpu: () => failing ? throw new InvalidOperationException() : 40,
            memory: () => failing ? throw new InvalidOperationException() : new MemorySample(50, 4UL << 30, 8UL << 30, 0, 0),
            network: () => failing ? throw new InvalidOperationException() : new NetworkSample(5, 2));
        var page = new DashboardViewModel(
            metrics, StubHardwareProviders.With(), () => new FakeGpuUsageSampler(), new FakeDiskThroughputSampler());
        page.SetActive(true);
        foreach (var timer in timers) timer.RaiseTick();
        var cpu = page.Cards.Single(c => c.Category == DeviceCategory.Cpu);
        var memory = page.Cards.Single(c => c.Category == DeviceCategory.Memory);
        var network = page.Cards.Single(c => c.Category == DeviceCategory.Network);
        Assert.Equal("%", cpu.Unit);
        Assert.NotEmpty(network.Sub);

        failing = true;
        foreach (var timer in timers) timer.RaiseTick();

        Assert.Equal("", cpu.Unit);
        Assert.Equal("", memory.Unit);
        Assert.Equal("", memory.Sub);
        Assert.Equal("", network.Unit);
        Assert.Equal("", network.Sub);
        Assert.Equal("", page.MemoryUtilizationText);
        Assert.Equal("", page.NetworkSubText);
    }

    [Fact]
    public void ASamplerThatRecoversAfterFailing_RestoresTheCardUnits() {
        var failing = true;
        var metrics = TestMetrics.WithTimers(
            out var timers, cpu: () => failing ? throw new InvalidOperationException() : 40);
        var page = new DashboardViewModel(
            metrics, StubHardwareProviders.With(), () => new FakeGpuUsageSampler(), new FakeDiskThroughputSampler());
        page.SetActive(true);
        timers[TestMetrics.CpuTimer].RaiseTick();
        var cpu = page.Cards.Single(c => c.Category == DeviceCategory.Cpu);
        Assert.Equal("", cpu.Unit);

        // The channel stops after a failure; a refresh polls once more and the unit comes back with the value.
        failing = false;
        page.RefreshNow();

        Assert.Equal("%", cpu.Unit);
    }
}
