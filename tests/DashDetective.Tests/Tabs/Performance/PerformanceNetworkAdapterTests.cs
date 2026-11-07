using DashDetective.Services.Network;
using DashDetective.Services.Theming;
using DashDetective.Tabs.Performance;
using DashDetective.Tests.Fakes;
using System.Linq;
using Xunit;

namespace DashDetective.Tests.Tabs.Performance;

/// <summary>Pins that the Network row follows the live adapter with its saved-order id fixed, and the Errors tile dashes when unreadable.</summary>
public class PerformanceNetworkAdapterTests {
    private static PerformanceViewModel Page(out System.Collections.Generic.List<FakeUiTimer> timers, System.Func<string> adapter) =>
        new(TestMetrics.WithTimers(out timers, network: () => new NetworkSample(1, 1), adapterName: adapter),
            StubHardwareProviders.With(), () => new FakeGpuUsageSampler());

    [Fact]
    public void NetworkTick_AfterTheSamplerSwitchesAdapter_RenamesTheRowAndRetargetsItsLink() {
        var adapter = "Ethernet";
        var page = Page(out var timers, () => adapter);
        page.SetActive(true);
        var row = page.Resources.First(r => r.Series == ChartSeries.NetDown);
        string? asked = null;
        page.NetworkRevealRequested += name => asked = name;

        adapter = "Wi-Fi";
        timers[TestMetrics.NetworkTimer].RaiseTick();
        row.Link!.Command.Execute(null);

        Assert.Equal("Wi-Fi", row.Name);
        Assert.Equal("Wi-Fi", asked);
        Assert.Equal("Ethernet", row.WidgetId);
    }

    [Fact]
    public void NetworkTick_WithNoReadableAdapter_ShowsADashForErrors() {
        var page = Page(out var timers, () => "No Such Adapter");
        page.SetActive(true);

        timers[TestMetrics.NetworkTimer].RaiseTick();

        var errors = page.Resources.First(r => r.Series == ChartSeries.NetDown).Stats.Single(t => t.Label == "Errors");
        Assert.Equal("—", errors.Value);
    }
}
