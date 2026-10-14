using Avalonia.Input;
using DashDetective.Services.SystemMetrics;
using DashDetective.Tabs.Dashboard;
using DashDetective.Tests.Fakes;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace DashDetective.Tests.Tabs.Dashboard;

/// <summary>
/// Pins <see cref="DashboardLink"/>'s gate: a single click on a Dashboard card or chart stays on the page,
/// while a double click or the keyboard opens the device in Performance — exactly once.
/// </summary>
public class DashboardLinkTests {
    private static DashboardViewModel Page() => new(TestMetrics.Idle(), StubHardwareProviders.With());

    private static List<string> Watch(DashboardViewModel page) {
        var asked = new List<string>();
        page.PerformanceRevealRequested += asked.Add;
        return asked;
    }

    [Theory]
    [InlineData(LinkGesture.Click, false)]
    [InlineData(LinkGesture.DoubleClick, true)]
    [InlineData(LinkGesture.Key, true)]
    public void Goes_OnlyTheSingleClickStays(LinkGesture gesture, bool expected) =>
        Assert.Equal(expected, DashboardLink.Goes(gesture));

    [Fact]
    public void Respond_SingleClickOnACard_DoesNotNavigate() {
        var page = Page();
        var asked = Watch(page);
        var link = new DashboardLink { Command = page.Cards.First(c => c.Category == DeviceCategory.Cpu).OpenCommand };

        link.Respond(LinkGesture.Click);

        Assert.Empty(asked);
    }

    [Fact]
    public void Respond_DoubleClickOnACard_OpensItsDeviceOnce() {
        var page = Page();
        var asked = Watch(page);
        var link = new DashboardLink { Command = page.Cards.First(c => c.Category == DeviceCategory.Memory).OpenCommand };

        link.Respond(LinkGesture.DoubleClick);

        Assert.Equal([DeviceIds.Memory], asked);
    }

    [Fact]
    public void Respond_SingleClickOnAChart_DoesNotNavigate() {
        var page = Page();
        var asked = Watch(page);
        var link = new DashboardLink { Command = page.OpenCpuChartCommand };

        link.Respond(LinkGesture.Click);

        Assert.Empty(asked);
    }

    [Fact]
    public void Respond_DoubleClickOnAChart_OpensItsDevice() {
        var page = Page();
        var asked = Watch(page);
        var link = new DashboardLink { Command = page.OpenNetworkChartCommand };

        link.Respond(LinkGesture.DoubleClick);

        Assert.Equal([DeviceIds.Network], asked);
    }

    /// <summary>Enter on a focused card still opens it: the keyboard has no single-click to guard against.</summary>
    [Fact]
    public void EnterOnAFocusedLink_OpensItsDevice() {
        var page = Page();
        var asked = Watch(page);
        var link = new DashboardLink { Command = page.OpenMemoryChartCommand };

        link.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });

        Assert.Equal([DeviceIds.Memory], asked);
    }
}
