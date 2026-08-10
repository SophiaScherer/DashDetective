using DashDetective.Tabs.Processes;
using Xunit;

namespace DashDetective.Tests.Tabs.Processes;

/// <summary>Pins that the strings <see cref="LinuxProcessSnapshotProvider.StatusFor"/> produces land
/// correctly on <see cref="ProcessRow"/>'s status dot, which tints green on the exact word "Running" and
/// amber on anything else.</summary>
public class ProcessRowStatusTests {
    private static ProcessRow Row(string status) =>
        new(new ProcessInfo(1, 0, "proc", status, 0, 0, 1, ProcessCategory.Background, 0, 0),
            depth: 0, hasChildren: false, isExpanded: false);

    /// <summary>R, S and I all collapse to "Running", so an ordinary sleeping process reads green — the
    /// same as on Windows, where almost every row reads "Running".</summary>
    [Fact]
    public void RunningStatus_UsesTheGreenBrush() =>
        Assert.Same(Row("Running").StatusBrush, Row(LinuxProcessSnapshotProvider.StatusFor('S')).StatusBrush);

    /// <summary>The three states worth noticing get the amber brush, exactly as a hung Windows app does.
    /// A misspelling here would silently tint every Linux row green.</summary>
    [Theory]
    [InlineData('D')]
    [InlineData('T')]
    [InlineData('Z')]
    public void NotableStates_UseTheWarningBrush(char state) {
        var row = Row(LinuxProcessSnapshotProvider.StatusFor(state));

        Assert.NotSame(Row("Running").StatusBrush, row.StatusBrush);
    }
}
