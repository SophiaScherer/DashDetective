using Avalonia;
using Avalonia.Controls;
using DashDetective.Shared.Layout;
using System;
using System.Collections.Generic;
using Xunit;

namespace DashDetective.Tests.Shared.Layout;

/// <summary>Covers <see cref="DropTarget"/>: a widget held between two slots settles on one, the target
/// changes only once the drag is a margin past the boundary, and the preview moving cannot feed back
/// into the answer, which is what made a held widget flicker.</summary>
public class DropTargetTests {
    private const double Gutter = 16;

    // Two rows of three 300x100 slots, the shape WidgetBoardLayoutTests measures SlotAt against.
    private static Rect2[] Grid(double scale = 1) => new[] {
        Box(0, 0, 300, 100, scale), Box(316, 0, 300, 100, scale), Box(632, 0, 300, 100, scale),
        Box(0, 116, 300, 100, scale), Box(316, 116, 300, 100, scale), Box(632, 116, 300, 100, scale),
    };

    private static readonly int[] GridRows = { 3, 6 };

    // The margin on the 300x100 grid: a fifth of the smaller slot on each axis.
    private const double MarginX = 300 * DropTarget.Hysteresis;
    private const double MarginY = 100 * DropTarget.Hysteresis;

    private static Rect2 Box(double left, double top, double width, double height, double scale) =>
        new(left * scale, top * scale, width * scale, height * scale);

    /// <summary>One row laid out left to right at these widths, the way a board arranges it.</summary>
    private static Rect2[] Row(params double[] widths) {
        var slots = new Rect2[widths.Length];
        var x = 0.0;
        for (var i = 0; i < widths.Length; i++) {
            slots[i] = new Rect2(x, 0, widths[i], 100);
            x += widths[i] + Gutter;
        }
        return slots;
    }

    private static DropTarget Started(Rect2[] slots, int[] rowEnds, int start) {
        var target = new DropTarget();
        target.Begin(slots, rowEnds, start);
        return target;
    }

    // ===== The loop it breaks =====

    /// <summary>The mechanism behind the flicker, pinned so it is not reintroduced: a narrow widget
    /// held over a wide one is over the wide one's slot in BOTH orders, so measuring against the live
    /// layout has no stable answer and every pointer move flipped it.</summary>
    [Fact]
    public void SlotAt_LiveLayoutOfANarrowWidgetOverAWideOne_FlipsWithTheOrder() {
        var narrowFirst = Row(300, 600);
        var wideFirst = Row(600, 300);

        Assert.Equal(1, WidgetBoardLayout.SlotAt(narrowFirst, new[] { 2 }, 450, 50));
        Assert.Equal(0, WidgetBoardLayout.SlotAt(wideFirst, new[] { 2 }, 450, 50));
    }

    /// <summary>The regression itself: held where the live layout oscillated, jittering, while the
    /// preview is moved after every reading exactly as a drag moves it. It moves once and settles.</summary>
    [Fact]
    public void Update_HeldBetweenTwoSlotsWhileThePreviewMoves_SettlesOnOne() {
        var target = Started(Row(300, 600), new[] { 2 }, 0);
        var order = new ChildOrder();
        order.Sync(2);
        order.BeginPreview();

        var targets = new HashSet<int>();
        var moves = 0;
        for (var i = 0; i < 200; i++) {
            var t = target.Update(450 + (i % 3 - 1), 50);
            targets.Add(t);
            if (order.Move(0, t, _ => true))
                moves++;
        }

        Assert.Equal(1, Assert.Single(targets));
        Assert.Equal(1, moves);
    }

    // ===== Hysteresis at a boundary =====

    /// <summary>Exactly on the boundary between two slots, and a DIP either side of it, the drag keeps
    /// the slot it came from — from whichever side it came.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Update_JitteringOnTheBoundary_KeepsTheSlotItCameFrom(int from) {
        var target = Started(Grid(), GridRows, from);

        for (var i = 0; i < 300; i++)
            Assert.Equal(from, target.Update(316 + (i % 3 - 1), 50));
    }

    [Fact]
    public void Update_WellPastTheBoundary_Switches() {
        var target = Started(Grid(), GridRows, 0);

        Assert.Equal(0, target.Update(316 + MarginX - 1, 50));
        Assert.Equal(1, target.Update(316 + MarginX + 1, 50));
    }

    [Fact]
    public void Update_BackPastTheBoundary_SwitchesBackOnlyOnceClearOfIt() {
        var target = Started(Grid(), GridRows, 0);
        target.Update(466, 50);

        Assert.Equal(1, target.Update(316 - MarginX + 1, 50));
        Assert.Equal(0, target.Update(316 - MarginX - 1, 50));
    }

    /// <summary>A drag that lands well inside a slot two along is not held by the margin of the one it
    /// left, so a fast sweep is answered where it ends.</summary>
    [Fact]
    public void Update_SweptAcrossSeveralSlots_TakesTheOneItEndsOver() {
        var target = Started(Grid(), GridRows, 0);

        Assert.Equal(2, target.Update(800, 50));
    }

    // ===== Wrapped rows =====

    /// <summary>Straight down into the next row still works — the gesture the board's slot rule exists
    /// for — but only once the drag is a margin into it.</summary>
    [Fact]
    public void Update_StraightDown_TakesTheSlotBelowOnceClearOfTheRow() {
        var target = Started(Grid(), GridRows, 0);

        Assert.Equal(0, target.Update(150, 100 + MarginY - 1));
        Assert.Equal(3, target.Update(150, 100 + MarginY + 1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void Update_JitteringOnARowBoundary_KeepsTheSlotItCameFrom(int from) {
        var target = Started(Grid(), GridRows, from);

        for (var i = 0; i < 300; i++)
            Assert.Equal(from, target.Update(150, 100 + (i % 3 - 1)));
    }

    /// <summary>From the end of one row to the start of the next, the wrap a narrow window makes of a
    /// single list.</summary>
    [Fact]
    public void Update_FromARowsLastSlotToTheNextRowsFirst_Switches() {
        var target = Started(Grid(), GridRows, 2);

        Assert.Equal(2, target.Update(800, 100 + MarginY - 1));
        Assert.Equal(3, target.Update(150, 166));
    }

    // ===== Interface size =====

    /// <summary>The margin is a fraction of the slots, so a board scaled up keeps the same answers at
    /// the same relative positions, and a one-DIP jitter still cannot flip it.</summary>
    [Theory]
    [InlineData(0.8)]
    [InlineData(1.0)]
    [InlineData(2.0)]
    public void Update_AtAnyScale_SwitchesAtTheSameRelativePoint(double scale) {
        var target = Started(Grid(scale), GridRows, 0);

        for (var i = 0; i < 100; i++)
            Assert.Equal(0, target.Update(316 * scale + (i % 3 - 1), 50 * scale));

        Assert.Equal(0, target.Update((316 + MarginX - 1) * scale, 50 * scale));
        Assert.Equal(1, target.Update((316 + MarginX + 1) * scale, 50 * scale));
    }

    // ===== Edges =====

    [Fact]
    public void Resolve_WithNoCurrentSlot_IsTheSlotUnderThePoint() {
        Assert.Equal(1, DropTarget.Resolve(Grid(), GridRows, 320, 50, -1));
    }

    [Fact]
    public void Update_BeforeBegin_IsTheSlotUnderThePoint() {
        Assert.Equal(0, new DropTarget().Update(10, 10));
    }

    [Fact]
    public void Begin_SnapshotsTheSlots_SoLaterChangesToTheSourceAreIgnored() {
        var slots = Row(300, 600);
        var target = Started(slots, new[] { 2 }, 0);
        slots[1] = new Rect2(5000, 0, 10, 100);

        Assert.Equal(1, target.Update(450, 50));
    }

    // ===== Keyboard reordering =====

    /// <summary>The keyboard path does not go through a drop target at all: one press still moves the
    /// focused item exactly one slot and persists through the same commit a drag does.</summary>
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
