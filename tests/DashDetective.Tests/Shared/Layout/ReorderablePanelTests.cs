using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using DashDetective.Shared.Layout;
using System;
using System.Collections.Generic;
using Xunit;

namespace DashDetective.Tests.Shared.Layout;

/// <summary>Covers <see cref="ReorderablePanel"/> on a real headless <see cref="WidgetBoard"/>: the keyboard
/// path moves by one index and never asks a drop target anything, and a drag's motion — neighbours slide
/// as it re-packs, the dropped item eases in — runs only for a drag, never for a cancelled one, a keyboard
/// move or with Reduce motion on.</summary>
public class ReorderablePanelTests {
    private const double Width = 1000;

    [Fact]
    public void TryMoveFocused_OneSlotRight_CommitsTheNeighboringOrder() {
        var (board, items) = Board();

        IReadOnlyList<string>? committed = null;
        board.OrderChanged += ids => committed = ids;

        Assert.True(board.TryMoveFocused(items[0], 1));
        Assert.Equal(new[] { "b", "a", "c" }, committed);
    }

    /// <summary>The Dashboard's card row has no id. A keyboard move that falls through to the board from
    /// the row's last card must not carry the whole row in among the charts.</summary>
    [Fact]
    public void TryMoveFocused_OnAChildWithNoId_LeavesItInPlace() {
        var (board, items) = Board();
        var row = new Border { MinWidth = 100, Height = 100 };
        WidgetBoard.SetStretch(row, true);
        board.Children.Insert(0, row);
        Layout(board);
        var moved = false;
        board.OrderChanged += _ => moved = true;

        Assert.False(board.TryMoveFocused(row, 1));
        Assert.False(moved);
        Assert.Same(row, ((IReorderablePanel)board).Items[0]);
        Assert.Same(items[0], ((IReorderablePanel)board).Items[1]);
    }

    /// <summary>Three equal widgets in one row, on a clock the test steps.</summary>
    private static (WidgetBoard Board, List<Border> Items) Board() {
        var board = new WidgetBoard { ColumnSpacing = 16 };
        var items = new List<Border>();
        foreach (var id in new[] { "a", "b", "c" }) {
            var item = new Border { MinWidth = 100, Height = 100 };
            WidgetBoard.SetMaxSlotWidth(item, 300);
            Reorder.SetId(item, id);
            board.Children.Add(item);
            items.Add(item);
        }
        Layout(board);
        return (board, items);
    }

    // There is no layout manager headless, so each move is followed by the pass the app would run.
    private static void Layout(WidgetBoard board) {
        board.InvalidateMeasure();
        board.Measure(new Size(Width, double.PositiveInfinity));
        board.Arrange(new Rect(0, 0, Width, board.DesiredSize.Height));
    }

    /// <summary>Picks up the first widget and holds it over the second's slot, which shifts the second
    /// into the first's place.</summary>
    private static void DragFirstOverSecond(WidgetBoard board, List<Border> items) {
        var grab = new Point(items[0].Bounds.Center.X, 50);
        board.Drag.Press(new ReorderHandle(items[0], items[0], items[0]), grab);
        board.Drag.MoveTo(new Point(items[1].Bounds.Center.X, 50), null);
        Layout(board);
    }

    private static double ShiftX(Control item) =>
        Assert.IsType<TranslateTransform>(item.RenderTransform).X;

    [Fact]
    public void Drag_ShiftingANeighbor_SlidesItFromWhereItWas() {
        var (board, items) = Board();
        board.Clock = () => TimeSpan.Zero;
        var was = items[1].Bounds.X;

        DragFirstOverSecond(board, items);

        Assert.True(items[1].Bounds.X < was);
        Assert.Equal(was - items[1].Bounds.X, ShiftX(items[1]), 3);
        Assert.Null(items[2].RenderTransform);
    }

    [Fact]
    public void OnFrame_AfterTheDuration_LeavesTheNeighborInItsSlot() {
        var (board, items) = Board();
        var now = TimeSpan.Zero;
        board.Clock = () => now;
        DragFirstOverSecond(board, items);

        now = ReorderSlides.Duration;
        board.OnFrame();

        Assert.Null(items[1].RenderTransform);
    }

    [Fact]
    public void Release_EasesTheDroppedItemInFromWhereItWasLetGo() {
        var (board, items) = Board();
        board.Clock = () => TimeSpan.Zero;
        DragFirstOverSecond(board, items);
        var letGo = board.Drag.DragBox();

        board.Drag.Release(null);
        Layout(board);

        Assert.Equal(letGo.X - items[0].Bounds.X, ShiftX(items[0]), 3);
        Assert.NotEqual(0, ShiftX(items[0]));
        Assert.Equal(10, items[0].ZIndex);
    }

    [Fact]
    public void Release_ThenTheDuration_SettlesTheDroppedItemBackToTheFlow() {
        var (board, items) = Board();
        var now = TimeSpan.Zero;
        board.Clock = () => now;
        DragFirstOverSecond(board, items);
        board.Drag.Release(null);
        Layout(board);

        now = ReorderSlides.Duration;
        board.OnFrame();

        Assert.Null(items[0].RenderTransform);
        Assert.Equal(0, items[0].ZIndex);
    }

    /// <summary>A drag that is never dropped puts everything straight back, so nothing is left sliding.</summary>
    [Fact]
    public void Cancel_PutsEveryWidgetBackWithoutMotion() {
        var (board, items) = Board();
        board.Clock = () => TimeSpan.Zero;
        DragFirstOverSecond(board, items);

        board.Drag.Cancel();
        Layout(board);

        Assert.All(items, item => Assert.Null(item.RenderTransform));
    }

    [Fact]
    public void Drag_WithReduceMotion_NeverSlides() {
        var (board, items) = Board();
        board.Clock = () => TimeSpan.Zero;
        board.MotionReduced = () => true;

        DragFirstOverSecond(board, items);
        Assert.All(items, item => Assert.Null(item.RenderTransform));

        board.Drag.Release(null);
        Layout(board);
        Assert.All(items, item => Assert.Null(item.RenderTransform));
    }

    [Fact]
    public void TryMoveFocused_MovesWithoutMotion() {
        var (board, items) = Board();
        board.Clock = () => TimeSpan.Zero;

        Assert.True(board.TryMoveFocused(items[0], 1));
        Layout(board);

        Assert.All(items, item => Assert.Null(item.RenderTransform));
    }

    [Fact]
    public void Resize_MovesWithoutMotion() {
        var (board, items) = Board();
        board.Clock = () => TimeSpan.Zero;

        board.Measure(new Size(400, double.PositiveInfinity));
        board.Arrange(new Rect(0, 0, 400, board.DesiredSize.Height));

        Assert.All(items, item => Assert.Null(item.RenderTransform));
    }

    /// <summary>Picking up a neighbour that is still sliding keeps it above the others: stopping its slide
    /// must not drop it back under them.</summary>
    [Fact]
    public void Drag_ANeighborStillSliding_IsHeldAboveTheOthers() {
        var (board, items) = Board();
        board.Clock = () => TimeSpan.Zero;
        DragFirstOverSecond(board, items);
        board.Drag.Release(null);
        Layout(board);

        var grab = new Point(items[1].Bounds.Center.X, 50);
        board.Drag.Press(new ReorderHandle(items[1], items[1], items[1]), grab);
        board.Drag.MoveTo(grab + new Point(40, 0), null);
        Layout(board);

        Assert.Equal(10, items[1].ZIndex);
        Assert.Null(items[1].RenderTransform);
    }

    /// <summary>A slide whose frames never came — the page was left mid-slide — is finished by the next
    /// layout rather than left drawn off its slot.</summary>
    [Fact]
    public void Layout_AfterAnExpiredSlideWithNoFrames_LandsTheWidget() {
        var (board, items) = Board();
        var now = TimeSpan.Zero;
        board.Clock = () => now;
        DragFirstOverSecond(board, items);
        board.Drag.Release(null);
        Layout(board);

        now = ReorderSlides.Duration;
        Layout(board);

        Assert.All(items, item => Assert.Null(item.RenderTransform));
        Assert.Equal(0, items[0].ZIndex);
    }

    /// <summary>Capture-lost is a direct event on the panel holding the capture; it must reach the drag, or
    /// a drag whose capture is taken away stays live until a later release commits it.</summary>
    [Fact]
    public void CaptureLost_MidDrag_CancelsTheDrag() {
        var (board, items) = Board();
        board.Clock = () => TimeSpan.Zero;
        board.Drag.Attach();
        DragFirstOverSecond(board, items);

        board.RaiseEvent(new PointerCaptureLostEventArgs(board, new Pointer(1, PointerType.Mouse, true)));
        Layout(board);

        Assert.False(board.Drag.IsDragging);
        Assert.All(items, item => Assert.Null(item.RenderTransform));
    }
}
