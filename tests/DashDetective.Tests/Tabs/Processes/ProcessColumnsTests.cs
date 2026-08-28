using DashDetective.Tabs.Processes;
using System;
using Xunit;

namespace DashDetective.Tests.Tabs.Processes;

/// <summary>Covers <see cref="ProcessColumns"/>' one structural invariant: its metrics table is indexed
/// by the enum, so the two must stay in the same order.</summary>
public class ProcessColumnsTests {
    [Fact]
    public void DefaultOrder_MatchesTheEnumsOwnOrder() {
        Assert.Equal(Enum.GetValues<ProcessColumnId>(), ProcessColumns.DefaultOrder);
    }

    [Fact]
    public void EveryColumn_HasAMinimumAndAWeight() {
        foreach (var id in ProcessColumns.DefaultOrder) {
            Assert.True(ProcessColumns.MinWidthOf(id) > 0, $"{id} has no minimum width");
            Assert.True(ProcessColumns.WeightOf(id) > 0, $"{id} has no weight");
        }
    }
}
