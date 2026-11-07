using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
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

    /// <summary>A chart dragged up over an unnamed card row stops below it.</summary>
    [Fact]
    public void MoveTo_AWidgetOverAChildWithNoId_StopsBelowIt() {
        var row = new Border { MinWidth = 200, Height = 100 };
        WidgetBoard.SetStretch(row, true);
        var chart = Widget("chart", 1, 200, 400);
        var board = Board(row, Widget("first", 1, 200, 400), chart);
        Press(board, chart, chart.Bounds.Center);

        board.Drag.MoveTo(new Point(chart.Bounds.Center.X, 20), null);
        Layout(board);
        board.Drag.Release(null);
        Layout(board);

        Assert.Equal(" chart first", Shown(board));
    }

    /// <summary>Detaching ends the press, or a release on another page leaves it pending.</summary>
    [Fact]
    public void Detach_WithAPressPending_DropsIt() {
        var (board, narrow) = NarrowBesideWide();
        Press(board, narrow, new Point(150, 50));

        board.Drag.Detach();
        board.Drag.MoveTo(new Point(450, 50), null);

        Assert.False(board.Drag.IsDragging);
    }

    /// <summary>A move with the button already up is not a drag, whatever was left pending.</summary>
    [Fact]
    public void PointerMoved_WithNoButtonHeld_DropsAPendingPress() {
        var (board, narrow) = NarrowBesideWide();
        board.Drag.Attach();
        Press(board, narrow, new Point(150, 50));

        board.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, board,
                                              new Pointer(1, PointerType.Mouse, true), board,
                                              new Point(450, 50), 0, new PointerPointProperties(),
                                              KeyModifiers.None));

        Assert.False(board.Drag.IsDragging);
        board.Drag.MoveTo(new Point(450, 50), null);
        Assert.False(board.Drag.IsDragging);
    }

    /// <summary>A confined strip keeps its dragged item inside itself.</summary>
    [Fact]
    public void DragBox_InAConfiningPanel_StaysInsideIt() {
        var (board, narrow) = NarrowBesideWide();
        board.ConfineDrag = true;
        Press(board, narrow, new Point(150, 50));

        board.Drag.MoveTo(new Point(150, 600), null);
        Layout(board);

        var box = board.Drag.DragBox();
        Assert.True(board.Drag.IsDragging);
        Assert.Equal(board.Bounds.Height - box.Height, box.Y, 6);
        Assert.Equal(0, box.X, 6);
    }

    [Fact]
    public void DragBox_InAPanelThatDoesNotConfine_FollowsThePointerOut() {
        var (board, narrow) = NarrowBesideWide();
        Press(board, narrow, new Point(150, 50));

        board.Drag.MoveTo(new Point(150, 600), null);
        Layout(board);

        Assert.Equal(550, board.Drag.DragBox().Y, 6);
    }
}
