using DashDetective.Services.Network;
using DashDetective.Services.Settings;
using DashDetective.Services.SystemMetrics;
using DashDetective.Services.Theming;
using DashDetective.Shared;
using DashDetective.Tabs.Performance;
using DashDetective.Tests.Fakes;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace DashDetective.Tests.Tabs.Performance;

/// <summary>Covers the alert bell on the Performance detail header: each row carries the shared switch
/// for its kind — every disk the one disk-activity switch, every GPU the one GPU switch — the adapter row
/// carries none, and a flip on a row is the shell's own instance flipping.</summary>
public class PerformanceAlertTests {
    private const string Amd = "0000:03:00.0";

    private static readonly IReadOnlyList<PhysicalDiskInfo> TwoDisks = [
        new(0, "Boot Drive", "NVMe SSD", 1_000_000_000_000, true),
        new(3, "Data Drive", "NVMe SSD", 2_000_000_000_000, false),
    ];

    private static async Task<PerformanceViewModel> LoadedAsync(AlertSwitches alerts) {
        var samplers = new MetricSamplers(
            () => 0, () => new MemorySample(0, 0, 0, 0, 0), () => new NetworkSample(0, 0), () => "TestNIC");

        var page = new PerformanceViewModel(
            new SystemMetricsService(samplers, () => new FakeUiTimer()),
            StubHardwareProviders.With(
                disks: TwoDisks,
                gpuAdapters: [new GpuAdapter(Amd, "AMD amdgpu (1002:73df)", false, 0)]),
            () => new FakeGpuUsageSampler().Reporting(Amd, 37), alerts: alerts);

        // The constructor fires its own load and forgets it; letting that finish keeps the row set stable.
        await Task.Delay(100);
        await page.LoadInventoryAsync();
        page.ShowAllDevices = true;
        return page;
    }

    [Fact]
    public async Task Rows_CarryTheSwitchForTheirKind() {
        var alerts = new AlertSwitches();
        var page = await LoadedAsync(alerts);

        Assert.Same(alerts.Cpu, Row(page, ChartSeries.Cpu).Alert);
        Assert.Same(alerts.Memory, Row(page, ChartSeries.Memory).Alert);
        Assert.Null(Row(page, ChartSeries.NetDown).Alert);

        var disks = page.Resources.Where(row => row.Series == ChartSeries.Storage).ToList();
        Assert.Equal(2, disks.Count);
        Assert.All(disks, row => Assert.Same(alerts.DiskActivity, row.Alert));

        Assert.Same(alerts.Gpu, Row(page, ChartSeries.Gpu).Alert);
    }

    [Fact]
    public async Task FlippingARowsBell_FlipsTheSharedSwitch() {
        var alerts = new AlertSwitches();
        var changes = 0;
        alerts.Changed += () => changes++;
        var page = await LoadedAsync(alerts);

        Row(page, ChartSeries.Gpu).Alert!.IsOn = true;

        Assert.True(alerts.Gpu.IsOn);
        Assert.True(alerts.CaptureInto(AppSettings.Defaults).AlertGpuEnabled);
        Assert.Equal(1, changes);
    }

    /// <summary>With the master switch off in Settings, the bell the page exposes reports itself unusable.</summary>
    [Fact]
    public async Task MasterOff_LeavesTheRowsBellUnavailable() {
        var alerts = new AlertSwitches();
        alerts.Reflect(AppSettings.Defaults with { ResourceAlerts = true });
        var page = await LoadedAsync(alerts);

        alerts.Reflect(AppSettings.Defaults with { ResourceAlerts = false });

        Assert.False(Row(page, ChartSeries.Cpu).Alert!.IsAvailable);
    }

    private static ResourceRow Row(PerformanceViewModel page, ChartSeries series) =>
        page.Resources.First(row => row.Series == series);
}
