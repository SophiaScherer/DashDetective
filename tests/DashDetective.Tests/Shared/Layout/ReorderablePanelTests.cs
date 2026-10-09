using Avalonia;
using Avalonia.Controls;
using DashDetective.Shared.Layout;
using System.Collections.Generic;
using Xunit;

namespace DashDetective.Tests.Shared.Layout;

/// <summary>Covers <see cref="ReorderablePanel"/>'s keyboard path, driven on a real headless
/// <see cref="WidgetBoard"/>: it moves by one index and never asks a drop target anything.</summary>
public class ReorderablePanelTests {
    [Fact]
    public void TryMoveFocused_OneSlotRight_CommitsTheNeighboringOrder() {
        var board = new WidgetBoard();
        var items = new List<Border>();
        foreach (var id in new[] { "a", "b", "c" }) {
            var item = new Border { MinWidth = 100 };
            Reorder.SetId(item, id);
            board.Children.Add(item);
            items.Add(item);
        }
        board.Measure(new Size(1000, double.PositiveInfinity));
        board.Arrange(new Rect(0, 0, 1000, 100));

        IReadOnlyList<string>? committed = null;
        board.OrderChanged += ids => committed = ids;

        Assert.True(board.TryMoveFocused(items[0], 1));
        Assert.Equal(new[] { "b", "a", "c" }, committed);
    }
}
