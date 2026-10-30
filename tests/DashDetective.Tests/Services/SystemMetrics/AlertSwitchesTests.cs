using DashDetective.Services.Settings;
using DashDetective.Services.SystemMetrics;
using System.Linq;
using Xunit;

namespace DashDetective.Tests.Services.SystemMetrics;

/// <summary>Covers <see cref="AlertSwitches"/>, the one set of alert flags every bell shares: it ships the
/// settings defaults, a load is silent, a flip from any page is reported and lands in the captured
/// snapshot, and mirroring the thresholds and the master switch never moves a flag.</summary>
public class AlertSwitchesTests {
    [Fact]
    public void New_ShipsTheSettingsDefaults() {
        var switches = new AlertSwitches();

        Assert.True(switches.Cpu.IsOn);
        Assert.True(switches.Memory.IsOn);
        Assert.False(switches.Gpu.IsOn);
        Assert.False(switches.DiskActivity.IsOn);
        Assert.True(switches.DiskSpace.IsOn);
    }

    [Fact]
    public void All_IsOrderedByMetric() {
        var switches = new AlertSwitches();

        Assert.Equal(
            [AlertMetric.Cpu, AlertMetric.Memory, AlertMetric.Gpu, AlertMetric.DiskActivity, AlertMetric.DiskSpace],
            switches.All.Select(alert => alert.Metric));
    }

    [Fact]
    public void Load_SetsEveryFlagWithoutReportingAChange() {
        var switches = new AlertSwitches();
        var changes = 0;
        switches.Changed += () => changes++;

        switches.Load(AppSettings.Defaults with {
            AlertCpuEnabled = false,
            AlertGpuEnabled = true,
            AlertLowDiskFreeEnabled = false,
        });

        Assert.False(switches.Cpu.IsOn);
        Assert.True(switches.Gpu.IsOn);
        Assert.False(switches.DiskSpace.IsOn);
        Assert.Equal(0, changes);
    }

    [Fact]
    public void FlippingAnySwitch_ReportsAChange() {
        var switches = new AlertSwitches();
        var changes = 0;
        switches.Changed += () => changes++;

        switches.Gpu.IsOn = true;
        switches.DiskSpace.IsOn = false;

        Assert.Equal(2, changes);
    }

    /// <summary>The persistence half of the item: a bell flipped on a page is what the shell's capture
    /// writes, under the existing keys.</summary>
    [Fact]
    public void CaptureInto_AFlipFromAPage_IsInTheSnapshot() {
        var switches = new AlertSwitches();
        switches.Load(AppSettings.Defaults);

        switches.DiskActivity.IsOn = true;
        switches.Cpu.IsOn = false;
        var captured = switches.CaptureInto(AppSettings.Defaults);

        Assert.True(captured.AlertDiskActiveEnabled);
        Assert.False(captured.AlertCpuEnabled);
        Assert.True(captured.AlertMemoryEnabled);
        Assert.False(captured.AlertGpuEnabled);
        Assert.True(captured.AlertLowDiskFreeEnabled);
    }

    [Fact]
    public void CaptureInto_RoundTripsALoad_AndLeavesTheThresholdsAlone() {
        var stored = AppSettings.Defaults with {
            AlertCpuEnabled = false,
            AlertMemoryEnabled = false,
            AlertGpuEnabled = true,
            AlertDiskActiveEnabled = true,
            AlertLowDiskFreeEnabled = false,
            AlertGpuPercent = 77,
        };
        var switches = new AlertSwitches();
        switches.Load(stored);

        Assert.Equal(stored, switches.CaptureInto(stored with {
            AlertCpuEnabled = true,
            AlertGpuEnabled = false,
        }));
    }

    [Fact]
    public void Reflect_MirrorsTheRule_WithoutMovingAFlag() {
        var switches = new AlertSwitches();
        switches.Load(AppSettings.Defaults);
        var changes = 0;
        switches.Changed += () => changes++;

        switches.Reflect(AppSettings.Defaults with {
            ResourceAlerts = true,
            AlertCpuPercent = 70,
            AlertLowDiskFreePercent = 5,
            AlertSustainSeconds = 30,
        });

        Assert.Equal(70, switches.Cpu.Threshold);
        Assert.Equal(5, switches.DiskSpace.Threshold);
        Assert.All(switches.All, alert => Assert.Equal(30, alert.SustainSeconds));
        Assert.All(switches.All, alert => Assert.True(alert.IsAvailable));
        Assert.True(switches.Cpu.IsOn);
        Assert.False(switches.Gpu.IsOn);
        Assert.Equal(0, changes);
    }

    /// <summary>The master switch off disables every bell; the state each one shows is kept.</summary>
    [Fact]
    public void Reflect_MasterOff_MakesEverySwitchUnavailable() {
        var switches = new AlertSwitches();
        switches.Reflect(AppSettings.Defaults with { ResourceAlerts = true });

        switches.Reflect(AppSettings.Defaults with { ResourceAlerts = false });

        Assert.All(switches.All, alert => Assert.False(alert.IsAvailable));
        Assert.True(switches.Cpu.IsOn);
    }
}
