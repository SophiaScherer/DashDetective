using DashDetective.Services.SystemMetrics;
using Xunit;

namespace DashDetective.Tests.Services.SystemMetrics;

/// <summary>Covers <see cref="AlertSwitch"/>: a load is silent while a user flip reports, and the bell's
/// description states the state, the live rule and what a click does — or, with the master switch off,
/// that Settings is why it cannot be used.</summary>
public class AlertSwitchTests {
    private static AlertSwitch Switch(AlertMetric metric, bool isOn, bool available = true,
                                      int threshold = 90, int sustain = 10) {
        var alert = new AlertSwitches().All[(int)metric];
        alert.Load(isOn);
        alert.IsAvailable = available;
        alert.Threshold = threshold;
        alert.SustainSeconds = sustain;
        return alert;
    }

    [Fact]
    public void Load_DoesNotReportAToggle() {
        var alert = Switch(AlertMetric.Cpu, isOn: false);
        var toggles = 0;
        alert.Toggled += () => toggles++;

        alert.Load(true);

        Assert.True(alert.IsOn);
        Assert.Equal(0, toggles);
    }

    [Fact]
    public void Flip_ReportsAToggle() {
        var alert = Switch(AlertMetric.Cpu, isOn: true);
        var toggles = 0;
        alert.Toggled += () => toggles++;

        alert.IsOn = false;

        Assert.Equal(1, toggles);
    }

    [Fact]
    public void Description_On_StatesTheRuleAndTheClick() {
        Assert.Equal(
            "CPU alert on: warns when CPU use stays at or above 85% for 12 seconds. Click to turn it off.",
            Switch(AlertMetric.Cpu, isOn: true, threshold: 85, sustain: 12).Description);
    }

    [Fact]
    public void Description_Off_OffersToTurnItOn() {
        Assert.Equal(
            "GPU alert off. Click to warn when any GPU stays at or above 90% busy for 10 seconds.",
            Switch(AlertMetric.Gpu, isOn: false).Description);
    }

    /// <summary>Free space has no wait, so its rule names none.</summary>
    [Fact]
    public void Description_DiskSpace_NamesNoWait() {
        Assert.Equal(
            "Low disk space alert on: warns when any drive drops to 10% free or less. Click to turn it off.",
            Switch(AlertMetric.DiskSpace, isOn: true, threshold: 10).Description);
    }

    [Fact]
    public void Description_OneSecond_IsSingular() {
        Assert.Contains("for 1 second.", Switch(AlertMetric.Memory, isOn: true, sustain: 1).Description);
    }

    /// <summary>A disabled bell has to say why, or it reads as broken.</summary>
    [Fact]
    public void Description_MasterOff_PointsAtSettings() {
        Assert.Equal(
            "Disk activity alert off. Resource alerts are switched off in Settings.",
            Switch(AlertMetric.DiskActivity, isOn: false, available: false).Description);
    }

    [Fact]
    public void Description_FollowsAThresholdChange() {
        var alert = Switch(AlertMetric.Cpu, isOn: true);
        var raised = false;
        alert.PropertyChanged += (_, e) => raised |= e.PropertyName == nameof(AlertSwitch.Description);

        alert.Threshold = 70;

        Assert.True(raised);
        Assert.Contains("70%", alert.Description);
    }
}
