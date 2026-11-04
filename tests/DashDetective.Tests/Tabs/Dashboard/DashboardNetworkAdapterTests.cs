using DashDetective.Services.Network;
using DashDetective.Tabs.Dashboard;
using DashDetective.Tests.Fakes;
using Xunit;

namespace DashDetective.Tests.Tabs.Dashboard;

/// <summary>
/// Pins that the Network card's adapter caption follows the sampler when it moves to another adapter,
/// rather than keeping the name read at construction.
/// </summary>
public class DashboardNetworkAdapterTests {
    [Fact]
    public void NetworkTick_AfterTheSamplerSwitchesAdapter_ShowsTheNewAdapterName() {
        var adapter = "Ethernet";
        var metrics = TestMetrics.WithTimers(out var timers, network: () => new NetworkSample(1, 1), adapterName: () => adapter);
        var page = new DashboardViewModel(
            metrics, StubHardwareProviders.With(), () => new FakeGpuUsageSampler(), new FakeDiskThroughputSampler());
        page.SetActive(true);
        Assert.Equal("Ethernet", page.NetworkAdapterName);

        adapter = "Wi-Fi";
        timers[TestMetrics.NetworkTimer].RaiseTick();

        Assert.Equal("Wi-Fi", page.NetworkAdapterName);
    }
}
