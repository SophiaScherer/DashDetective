using Avalonia;
using Avalonia.Controls;
using DashDetective.Shared.Layout;
using System.Collections.Generic;
using Xunit;

namespace DashDetective.Tests.Shared.Layout;

/// <summary>Covers <see cref="ReorderDrag"/> on a real headless <see cref="WidgetBoard"/>, re-laid out
/// after every move as the app does: the drop target is read from slots snapshotted at drag start, so
/// the preview cannot feed back into it, and a child hidden mid-drag retakes the snapshot.</summary>
public class ReorderDragTests {
    private const double Width = 916;

    private static Border Widget(string id, double weight, double min, double max, bool stretch = false) {
        var widget = new Border { MinWidth = min, Height = 100 };
        WidgetBoard.SetWeight(widget, weight);
        WidgetBoard.SetMaxSlotWidth(widget, max);
        WidgetBoard.SetStretch(widget, stretch);
        Reorder.SetId(widget, id);
        return widget;
    }

    private static WidgetBoard Board(params Border[] widgets) {
        var board = new WidgetBoard { ColumnSpacing = 16, RowSpacing = 16 };
        foreach (var widget in widgets)
            board.Children.Add(widget);
        Layout(board);
        return board;
    }

    // There is no layout manager headless, so each move is followed by the pass the app would run.
    private static void Layout(WidgetBoard board) {
        board.InvalidateMeasure();
        board.Measure(new Size(Width, double.PositiveInfinity));
        board.Arrange(new Rect(0, 0, Width, board.DesiredSize.Height));
    }

    private static string Shown(WidgetBoard board) {
        var ids = new List<string>();
        foreach (var item in ((IReorderablePanel)board).Items)
            ids.Add(Reorder.IdOf(item));
        return string.Join(" ", ids);
    }

    private static void Press(WidgetBoard board, Border widget, Point at) =>
        board.Drag.Press(new ReorderHandle(widget, widget, widget), at);

    /// <summary>One row the board splits 300 / 600: a narrow widget beside a wide one.</summary>
    private static (WidgetBoard Board, Border Narrow) NarrowBesideWide() {
        var narrow = Widget("narrow", 1, 200, 300);
        var board = Board(narrow, Widget("wide", 2, 400, 600));
        return (board, narrow);
    }

    /// <summary>The regression: the narrow widget held, jittering, over the wide one's slot. Against
    /// the live slots this swapped back and forth forever; against the snapshot it swaps once.</summary>
    [Fact]
    public void MoveTo_HeldOverAWideNeighborAcrossReLayouts_SwapsOnceAndSettles() {
        var (board, narrow) = NarrowBesideWide();
        Press(board, narrow, new Point(150, 50));

        var orders = new List<string>();
        for (var i = 0; i < 50; i++) {
            board.Drag.MoveTo(new Point(450 + (i % 3 - 1), 50), null);
            Layout(board);
            orders.Add(Shown(board));
        }

        Assert.All(orders, order => Assert.Equal("wide narrow", order));
    }

    /// <summary>Why the snapshot is load-bearing: resolving the same held point against the live,
    /// re-laid-out slots never settles, even with the hysteresis margin.</summary>
    [Fact]
    public void Resolve_AgainstLiveReLaidOutSlots_Oscillates() {
        var (board, narrow) = NarrowBesideWide();
        var panel = (IReorderablePanel)board;
        Press(board, narrow, new Point(150, 50));
        board.Drag.MoveTo(new Point(157, 50), null);    // past the threshold, still over its own slot
        Layout(board);

        var current = 0;
        var targets = new List<int>();
        for (var i = 0; i < 6; i++) {
            current = DropTarget.Resolve(panel.SlotRects, panel.SlotRowEnds, 450, 50, current);
            targets.Add(current);
            panel.PreviewMove(narrow, current);
            Layout(board);
        }

        Assert.Equal(new[] { 1, 0, 1, 0, 1, 0 }, targets);
    }

    /// <summary>Hiding a child mid-drag renumbers the slots, so the snapshot is retaken. Held over its
    /// own place, the widget stays put; read against the stale snapshot it jumped past its neighbor.</summary>
    [Fact]
    public void MoveTo_AChildHiddenMidDrag_RetakesTheSnapshot() {
        var above = Widget("above", 1, 200, double.PositiveInfinity, stretch: true);
        var narrow = Widget("narrow", 1, 200, 300);
        var board = Board(above, narrow, Widget("wide", 2, 400, 600));
        Press(board, narrow, new Point(150, 166));
        board.Drag.MoveTo(new Point(157, 166), null);
        Layout(board);
        Assert.Equal("above narrow wide", Shown(board));

        above.IsVisible = false;
        Layout(board);
        for (var i = 0; i < 5; i++) {
            board.Drag.MoveTo(new Point(157 + (i % 3 - 1), 166), null);
            Layout(board);
        }

        Assert.Equal("narrow wide", Shown(board));
    }
}
