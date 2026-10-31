using DashDetective.Tabs.Settings;
using Xunit;

namespace DashDetective.Tests.Tabs.Settings;

/// <summary>Covers <see cref="AlertThresholdRow"/>: that seeding is silent, that edits report, and that a
/// stored number is clamped into the field's range. The row holds only the number; whether the resource is
/// watched is its bell's switch, covered by <c>AlertSwitchTests</c>.</summary>
public class AlertThresholdRowTests {
    private static AlertThresholdRow Row(int value) => new(value, 1, 100, "%", () => { });

    [Fact]
    public void Construction_DoesNotReportAChange() {
        var changes = 0;
        _ = new AlertThresholdRow(90, 1, 100, "%", () => changes++);

        Assert.Equal(0, changes);
    }

    [Fact]
    public void EditingTheValue_ReportsAChange() {
        var changes = 0;
        var row = new AlertThresholdRow(90, 1, 100, "%", () => changes++);

        row.Value = 75;

        Assert.Equal(1, changes);
    }

    /// <summary>A hand-edited settings file can hold anything, so the seed is clamped rather than trusted.</summary>
    [Theory]
    [InlineData(0, 1)]
    [InlineData(-40, 1)]
    [InlineData(9000, 100)]
    [InlineData(90, 90)]
    public void Seeding_ClampsIntoRange(int stored, int expected) {
        Assert.Equal(expected, Row(stored).Value);
    }
}
