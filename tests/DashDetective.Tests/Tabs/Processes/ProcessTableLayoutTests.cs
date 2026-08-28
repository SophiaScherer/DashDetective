using DashDetective.Tabs.Processes;
using System.Collections.Generic;
using Xunit;

namespace DashDetective.Tests.Tabs.Processes;

/// <summary>Covers <see cref="ProcessTableLayout"/>: every column is always present, the definitions
/// follow the order they are asked for, and the minimum table width below which the table scrolls
/// sideways rather than dropping one.</summary>
public class ProcessTableLayoutTests {
    [Fact]
    public void Definitions_InDeclaredOrder_KeepAllSevenColumns() {
        Assert.Equal("2.4*,0.7*,1*,0.85*,0.85*,0.85*,0.85*",
                     ProcessTableLayout.Definitions(ProcessColumns.DefaultOrder));
    }

    [Fact]
    public void Definitions_FollowTheOrderTheyAreGiven() {
        // The weights travel with their columns, so a dragged column keeps the width it had.
        IReadOnlyList<ProcessColumnId> order = new[] {
            ProcessColumnId.Name, ProcessColumnId.Cpu, ProcessColumnId.Pid,
            ProcessColumnId.Status, ProcessColumnId.Memory, ProcessColumnId.Disk, ProcessColumnId.Gpu,
        };

        Assert.Equal("2.4*,0.85*,0.7*,1*,0.85*,0.85*,0.85*", ProcessTableLayout.Definitions(order));
    }

    [Fact]
    public void Definitions_AlwaysHaveOneSlotPerColumn() {
        Assert.Equal(ProcessColumns.Count,
                     ProcessTableLayout.Definitions(ProcessColumns.DefaultOrder).Split(',').Length);
    }

    [Fact]
    public void MinTableWidth_ClearsEveryColumnMinimum() {
        // At exactly the minimum width, the weighted split must still give every column at least the
        // width ProcessColumns asks for — that is what makes it the scroll threshold.
        var order = ProcessColumns.DefaultOrder;
        var content = ProcessTableLayout.MinTableWidth - ProcessTableLayout.Spacing * (order.Count - 1);
        var total = 0d;
        foreach (var id in order)
            total += ProcessColumns.WeightOf(id);

        foreach (var id in order) {
            var share = content * ProcessColumns.WeightOf(id) / total;
            Assert.True(share >= ProcessColumns.MinWidthOf(id) - 0.001,
                        $"{id} got {share}, needs {ProcessColumns.MinWidthOf(id)}");
        }
    }

    [Fact]
    public void MinTableWidth_IsDrivenByTheTightestColumn() {
        // CPU has the worst minimum-to-weight ratio (72 / 0.85), so it is what sets the threshold.
        Assert.Equal(72d / 0.85 * 7.5 + 24, ProcessTableLayout.MinTableWidth, 3);
    }
}
