using DashDetective.Tabs.Settings;
using Xunit;

namespace DashDetective.Tests.Tabs.Settings;

/// <summary>Covers <see cref="AlertThresholdRow"/>: that seeding is silent, that edits report, and that a
/// switched-off row keeps its number — the reason the switch and the threshold are two values rather than
/// the settings layer's single zero-means-off one.</summary>
public class AlertThresholdRowTests {
    private static AlertThresholdRow Row(bool isEnabled, int value, int changes = 0) =>
        new(isEnabled, value, 1, 100, "%", () => { });

    [Fact]
    public void Construction_DoesNotReportAChange() {
        var changes = 0;
        _ = new AlertThresholdRow(true, 90, 1, 100, "%", () => changes++);

        Assert.Equal(0, changes);
    }

    [Fact]
    public void EditingTheValue_ReportsAChange() {
        var changes = 0;
        var row = new AlertThresholdRow(true, 90, 1, 100, "%", () => changes++);

        row.Value = 75;

        Assert.Equal(1, changes);
    }

    [Fact]
    public void TogglingTheSwitch_ReportsAChange() {
        var changes = 0;
        var row = new AlertThresholdRow(true, 90, 1, 100, "%", () => changes++);

        row.IsEnabled = false;

        Assert.Equal(1, changes);
    }

    /// <summary>The whole reason the pair exists. Storing only the settings layer's zero-means-off number
    /// would lose the threshold the moment the row was switched off.</summary>
    [Fact]
    public void SwitchingOff_KeepsTheNumber() {
        var row = Row(isEnabled: true, 75);

        row.IsEnabled = false;

        Assert.Equal(75, row.Value);
        Assert.Equal(0, row.EffectiveValue);

        row.IsEnabled = true;
        Assert.Equal(75, row.EffectiveValue);
    }

    /// <summary>What GPU and disk activity ship as: switched off, but with a usable number already in the
    /// box rather than an empty or zeroed field.</summary>
    [Fact]
    public void ADisabledRow_StillCarriesItsDefault() {
        var row = Row(isEnabled: false, 90);

        Assert.Equal(90, row.Value);
        Assert.Equal(0, row.EffectiveValue);
    }

    /// <summary>A hand-edited settings file can hold anything, so the seed is clamped rather than trusted.</summary>
    [Theory]
    [InlineData(0, 1)]
    [InlineData(-40, 1)]
    [InlineData(9000, 100)]
    [InlineData(90, 90)]
    public void Seeding_ClampsIntoRange(int stored, int expected) {
        Assert.Equal(expected, Row(isEnabled: true, stored).Value);
    }
}
