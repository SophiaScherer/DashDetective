using DashDetective.Services.SystemMetrics;
using DashDetective.Tabs.Dashboard;
using Xunit;

namespace DashDetective.Tests.Tabs.Dashboard;

/// <summary>Covers <see cref="DashboardCard"/>'s identity in the card row's saved order: named by device,
/// except a GPU, whose device id carries a LUID that changes every boot and so goes by its index label.</summary>
public class DashboardCardTests {
    private static DashboardCard Card(DeviceCategory category, string deviceId, string label) =>
        new(category, deviceId, label, "%", _ => { });

    [Fact]
    public void WidgetId_ForASingletonOrADisk_NamesTheDevice() {
        Assert.Equal("dashboard.card.cpu", Card(DeviceCategory.Cpu, DeviceIds.Cpu, "CPU").WidgetId);
        Assert.Equal("dashboard.card.disk:1", Card(DeviceCategory.Disk, DeviceIds.Disk(1), "DATA (D:)").WidgetId);
    }

    [Fact]
    public void WidgetId_ForAGpu_IgnoresTheLuid() {
        var today = Card(DeviceCategory.Gpu, DeviceIds.Gpu("0000000000012345"), "GPU 0");
        var afterReboot = Card(DeviceCategory.Gpu, DeviceIds.Gpu("000000000009abcd"), "GPU 0");

        Assert.Equal("dashboard.card.gpu 0", today.WidgetId);
        Assert.Equal(today.WidgetId, afterReboot.WidgetId);
    }
}
