using DashDetective.Tabs.Processes;
using Xunit;

namespace DashDetective.Tests.Tabs.Processes;

/// <summary>Covers <see cref="ProcessTableLayout"/>: every column is always present, and the minimum
/// table width below which the table scrolls sideways rather than dropping one.</summary>
public class ProcessTableLayoutTests {
    [Fact]
    public void Definitions_KeepAllSevenColumns() {
        Assert.Equal("2.4*,0.7*,1*,0.85*,0.85*,0.85*,0.85*", ProcessTableLayout.Definitions);
    }

    [Fact]
    public void Definitions_MatchTheWeightsAndMinimums() {
        // Cell Grid.Column indices are static, so all three tables must agree on the slot count.
        Assert.Equal(ProcessTableLayout.Minimums.Length, ProcessTableLayout.Weights.Length);
        Assert.Equal(ProcessTableLayout.Weights.Length, ProcessTableLayout.Definitions.Split(',').Length);
    }

    [Fact]
    public void MinTableWidth_ClearsEveryColumnMinimum() {
        // At exactly the minimum width, the weighted split must still give every column at least the
        // width its own entry in Minimums asks for — that is what makes it the scroll threshold.
        var content = ProcessTableLayout.MinTableWidth -
                      ProcessTableLayout.Spacing * (ProcessTableLayout.Weights.Length - 1);
        var total = 0d;
        foreach (var weight in ProcessTableLayout.Weights)
            total += weight;

        for (var i = 0; i < ProcessTableLayout.Weights.Length; i++) {
            var share = content * ProcessTableLayout.Weights[i] / total;
            Assert.True(share >= ProcessTableLayout.Minimums[i] - 0.001,
                        $"column {i} got {share}, needs {ProcessTableLayout.Minimums[i]}");
        }
    }

    [Fact]
    public void MinTableWidth_IsDrivenByTheTightestColumn() {
        // CPU has the worst minimum-to-weight ratio (72 / 0.85), so it is what sets the threshold.
        Assert.Equal(72d / 0.85 * 7.5 + 24, ProcessTableLayout.MinTableWidth, 3);
    }
}
