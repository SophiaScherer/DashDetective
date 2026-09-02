using DashDetective.Shared.Layout;
using System;
using Xunit;

namespace DashDetective.Tests.Shared.Layout;

/// <summary>Covers <see cref="WidgetBoardLayout"/>: which widgets share a row as the window widens,
/// how a row divides once caps are in play, and where the width nobody may take ends up.</summary>
public class WidgetBoardLayoutTests {
    private const double Gutter = 16;

    // The Network tab's six widgets, in page order, with the caps the view declares.
    private static WidgetSlot[] NetworkPage() => new[] {
        new WidgetSlot(1.0, 260, 520),    // Adapters
        new WidgetSlot(1.0, 260, 520),    // IP Configuration
        new WidgetSlot(1.1, 280, 620),    // Throughput
        new WidgetSlot(1.6, 420, 900),    // Active Connections
        new WidgetSlot(1.0, 260, 480),    // Ping
        new WidgetSlot(1.0, 260, 480),    // DNS Lookup
    };

    private static int[] RowSizes(WidgetSlot[] slots, double width) {
        var ends = WidgetBoardLayout.PackRows(slots, width, Gutter);
        var sizes = new int[ends.Count];
        var start = 0;
        for (var i = 0; i < ends.Count; i++) {
            sizes[i] = ends[i] - start;
            start = ends[i];
        }
        return sizes;
    }

    // ===== Packing =====

    [Fact]
    public void PackRows_Empty_HasNoRows() {
        Assert.Empty(WidgetBoardLayout.PackRows(Array.Empty<WidgetSlot>(), 1400, Gutter));
    }

    [Fact]
    public void PackRows_EveryWidgetLandsInExactlyOneRow_InOrder() {
        // Order is never rearranged to pack better: a dragged widget has to land where the user put it.
        foreach (var width in new double[] { 640, 900, 1180, 1400, 1920, 2560, 3440 }) {
            var ends = WidgetBoardLayout.PackRows(NetworkPage(), width, Gutter);
            Assert.Equal(6, ends[^1]);
            for (var i = 1; i < ends.Count; i++)
                Assert.True(ends[i] > ends[i - 1], $"row ends must advance at width {width}");
        }
    }

    [Fact]
    public void PackRows_NarrowWindow_StacksOneWidgetPerRow() {
        // Widths here are the page's CONTENT width, which the rail and the page inset take ~280px out
        // of. At the 640px window minimum that leaves ~500, where no two minimums fit side by side.
        Assert.Equal(new[] { 1, 1, 1, 1, 1, 1 }, RowSizes(NetworkPage(), 500));
    }

    [Fact]
    public void PackRows_DefaultWindow_KeepsThePageAsAuthored() {
        // ~1120 content is the shipped 1400px window: three across, then the table beside Ping and DNS,
        // which is the layout the page used to author by hand.
        Assert.Equal(new[] { 3, 3 }, RowSizes(NetworkPage(), 1120));
    }

    [Fact]
    public void PackRows_Ultrawide_SpendsSurplusOnColumnsNotWidth() {
        // The whole point: at 3440 every widget sits on one row rather than six panels stretching.
        Assert.Equal(new[] { 6 }, RowSizes(NetworkPage(), 3440));
    }

    [Fact]
    public void PackRows_ColumnsNeverDecreaseAsTheWindowWidens() {
        var previous = int.MaxValue;
        for (var width = 700.0; width <= 3600; width += 20) {
            var rows = RowSizes(NetworkPage(), width).Length;
            Assert.True(rows <= previous, $"row count rose from {previous} to {rows} at width {width}");
            previous = rows;
        }
    }

    [Fact]
    public void PackRows_BreakBefore_ClosesTheRowEarly() {
        var slots = NetworkPage();
        slots[2] = slots[2] with { BreakBefore = true };
        // Throughput would otherwise have joined the first row; instead it opens one and takes the
        // connections table with it, leaving Ping and DNS to pair off.
        Assert.Equal(new[] { 2, 2, 2 }, RowSizes(slots, 1120));
    }

    [Fact]
    public void PackRows_TrailingOrphan_JoinsTheRowAbove() {
        // Five widgets at this width pack 3 + 1 + 1; the last would otherwise span the page alone
        // beside a capped neighbour, which reads as a mistake rather than as a layout.
        var slots = NetworkPage()[..5];
        Assert.Equal(new[] { 3, 2 }, RowSizes(slots, 1120));
    }

    [Fact]
    public void PackRows_TrailingOrphan_StaysPutWhenItWillNotFit() {
        var slots = NetworkPage()[..3];
        Assert.Equal(new[] { 1, 1, 1 }, RowSizes(slots, 500));
    }

    [Fact]
    public void PackRows_StretchWidget_OwnsItsRow() {
        // A card strip spans the page, and pins whatever sits after it into a fresh row.
        var slots = new[] {
            new WidgetSlot(1, 150, double.PositiveInfinity, Stretch: true),
            new WidgetSlot(1, 260, 520),
            new WidgetSlot(1, 260, 520),
        };
        Assert.Equal(new[] { 1, 2 }, RowSizes(slots, 1400));
    }

    [Fact]
    public void PackRows_UncappedWidget_NeverPullsAnotherIn() {
        // Nothing declares a readable ceiling, so no width counts as surplus — and the trailing-orphan
        // merge stays out of it too, for the same reason: an uncapped widget owns its row on purpose.
        var slots = new[] {
            new WidgetSlot(1, 260, double.PositiveInfinity),
            new WidgetSlot(1, 260, double.PositiveInfinity),
        };
        Assert.Equal(new[] { 1, 1 }, RowSizes(slots, 3440));
    }

    [Fact]
    public void PackRows_InfiniteWidth_IsOneRow() {
        // An Auto slot can host anything, so nothing wraps.
        Assert.Equal(new[] { 6 }, RowSizes(NetworkPage(), double.PositiveInfinity));
    }

    // ===== Surplus =====

    [Fact]
    public void HasSurplus_TrueWhenTheRowExceedsWhatItsWidgetsCanUse() {
        var row = new[] { new WidgetSlot(1, 260, 520), new WidgetSlot(1, 260, 520) };
        Assert.True(WidgetBoardLayout.HasSurplus(row, 1200, Gutter));   // 1184 content > 1040 of cap
        Assert.False(WidgetBoardLayout.HasSurplus(row, 1000, Gutter));  // 984 content < 1040
    }

    [Fact]
    public void HasSurplus_FalseForAnUncappedOrStretchingWidget() {
        Assert.False(WidgetBoardLayout.HasSurplus(
            new[] { new WidgetSlot(1, 100, double.PositiveInfinity) }, 5000, Gutter));
        Assert.False(WidgetBoardLayout.HasSurplus(
            new[] { new WidgetSlot(1, 100, 200, Stretch: true) }, 5000, Gutter));
    }

    // ===== Splitting =====

    [Fact]
    public void SplitRow_BelowEveryCap_IsTheWeightedSplit() {
        // Unchanged from WeightedRowLayout, which this still defers to.
        var slots = new[] { new WidgetSlot(1.6, 420, 2000), new WidgetSlot(1, 260, 2000) };
        var widths = new double[2];
        WidgetBoardLayout.SplitRow(slots, 1316, Gutter, widths);
        Assert.Equal(800, widths[0], 6);
        Assert.Equal(500, widths[1], 6);
    }

    [Fact]
    public void SplitRow_CappedWidget_HandsTheSurplusToItsNeighbour() {
        // The 1.6-weight slot would take 800 but caps at 600, so the other absorbs the 200.
        var slots = new[] { new WidgetSlot(1.6, 420, 600), new WidgetSlot(1, 260, 2000) };
        var widths = new double[2];
        WidgetBoardLayout.SplitRow(slots, 1316, Gutter, widths);
        Assert.Equal(600, widths[0], 6);
        Assert.Equal(700, widths[1], 6);
    }

    [Fact]
    public void SplitRow_RedistributionReachesAFixedPoint() {
        // Handing the first cap's surplus over pushes the second over its own cap too.
        var slots = new[] {
            new WidgetSlot(2, 100, 300),
            new WidgetSlot(1, 100, 350),
            new WidgetSlot(1, 100, 2000),
        };
        var widths = new double[3];
        WidgetBoardLayout.SplitRow(slots, 1600 + Gutter * 2, Gutter, widths);
        Assert.Equal(300, widths[0], 6);
        Assert.Equal(350, widths[1], 6);
        Assert.Equal(950, widths[2], 6);
    }

    [Fact]
    public void SplitRow_EveryWidgetCapped_SharesTheLeftoverRatherThanBankingIt() {
        // The row is as full as packing could make it, so the caps give way: banking the leftover as
        // whitespace is the thing the caps were introduced to remove.
        var slots = new[] {
            new WidgetSlot(1, 100, 400),
            new WidgetSlot(1, 100, 400),
            new WidgetSlot(1, 100, 400),
        };
        var widths = new double[3];
        WidgetBoardLayout.SplitRow(slots, 1500, Gutter, widths);
        // 1500 − 2·16 gutters = 1468 content, split evenly.
        Assert.All(widths, w => Assert.Equal(1468.0 / 3, w, 6));
    }

    [Fact]
    public void SplitRow_TwoCappedWidgetsOnAWidePage_FillIt() {
        // The Storage tab on an ultrawide: two widgets and nothing else to buy a column with, so they
        // share the width by weight rather than leaving half the screen empty.
        var slots = new[] { new WidgetSlot(1.6, 440, 900), new WidgetSlot(1, 280, 620) };
        var widths = new double[2];
        WidgetBoardLayout.SplitRow(slots, 3120 + Gutter, Gutter, widths);
        Assert.Equal(3120, widths[0] + widths[1], 6);
        Assert.Equal(1920, widths[0], 6);
        Assert.Equal(1200, widths[1], 6);
    }

    [Fact]
    public void SplitRow_LoneWidget_TakesTheWholeWidthUncapped() {
        // No gutter can absorb a remainder here, and capping would leave a dead margin down one side.
        var widths = new double[1];
        WidgetBoardLayout.SplitRow(new[] { new WidgetSlot(1, 260, 520) }, 3000, Gutter, widths);
        Assert.Equal(3000, widths[0], 6);
    }

    [Fact]
    public void SplitRow_HonoursMinimumsOverWeight() {
        // The narrow slot's share falls under its minimum, so it is pinned there instead.
        var slots = new[] { new WidgetSlot(4, 100, 2000), new WidgetSlot(1, 300, 2000) };
        var widths = new double[2];
        WidgetBoardLayout.SplitRow(slots, 1000 + Gutter, Gutter, widths);
        Assert.Equal(300, widths[1], 6);
        Assert.Equal(700, widths[0], 6);
    }

    [Fact]
    public void SplitRow_SumsToTheContentWidth_LeavingNoSeam() {
        var slots = NetworkPage()[..3];
        var widths = new double[3];
        WidgetBoardLayout.SplitRow(slots, 1367, Gutter, widths);
        Assert.Equal(1367, widths[0] + widths[1] + widths[2] + Gutter * 2, 6);
    }

    [Fact]
    public void SplitRow_ZeroWidth_ProducesNoNegatives() {
        var widths = new double[2];
        WidgetBoardLayout.SplitRow(NetworkPage()[..2], 0, Gutter, widths);
        Assert.All(widths, w => Assert.True(w >= 0, "a width must never go negative"));
    }

    [Fact]
    public void SplitRow_NoWidgetExceedsItsCap_WhileAnotherColumnWasStillAffordable() {
        var slots = NetworkPage();
        var widths = new double[slots.Length];
        for (var width = 700.0; width <= 3600; width += 20) {
            var ends = WidgetBoardLayout.PackRows(slots, width, Gutter);
            var start = 0;
            foreach (var end in ends) {
                var count = end - start;
                var row = slots.AsSpan(start, count);
                WidgetBoardLayout.SplitRow(row, width, Gutter, widths.AsSpan(0, count));
                // Once the row is wider than every cap put together it shares the leftover out
                // instead, so only rows that could still honour their caps are checked.
                if (count > 1 && !ExceedsEveryCap(row, width))
                    for (var i = 0; i < count; i++)
                        Assert.True(widths[i] <= row[i].MaxWidth + 1e-6,
                                    $"widget {start + i} reached {widths[i]} over cap {row[i].MaxWidth} at {width}");
                start = end;
            }
        }
    }

    // ===== The slot a drag is over =====

    // Two rows of three 300px slots, 100px tall.
    private static Rect2[] Grid() => new[] {
        new Rect2(0, 0, 300, 100), new Rect2(316, 0, 300, 100), new Rect2(632, 0, 300, 100),
        new Rect2(0, 116, 300, 100), new Rect2(316, 116, 300, 100), new Rect2(632, 116, 300, 100),
    };

    private static readonly int[] GridRows = { 3, 6 };

    /// <summary>A drag takes the slot it covers. Measured from the box it occupies rather than from the
    /// pointer inside it, so where in the item it was grabbed cannot change the answer.</summary>
    [Theory]
    [InlineData(150, 50, 0)]      // over the first slot
    [InlineData(10, 50, 0)]       // and still over it near its leading edge
    [InlineData(466, 50, 1)]      // over the second
    [InlineData(800, 50, 2)]      // over the third
    [InlineData(150, 166, 3)]     // straight below the first: the second row's first slot
    [InlineData(800, 150, 5)]     // the second row's last
    public void SlotAt_TakesTheSlotTheDragCovers(double x, double y, int expected) {
        Assert.Equal(expected, WidgetBoardLayout.SlotAt(Grid(), GridRows, x, y));
    }

    /// <summary>Straight down is the gesture the old gap reading could not express: x never changes, so
    /// which side of a slot's middle the drag sat on could never move it into the row below.</summary>
    [Fact]
    public void SlotAt_StraightDown_TakesTheSlotBelow() {
        var grid = Grid();
        Assert.Equal(0, WidgetBoardLayout.SlotAt(grid, GridRows, 150, 50));
        Assert.Equal(3, WidgetBoardLayout.SlotAt(grid, GridRows, 150, 166));
    }

    [Fact]
    public void SlotAt_InAGutter_KeepsTheSlotBeforeIt() {
        Assert.Equal(0, WidgetBoardLayout.SlotAt(Grid(), GridRows, 308, 50));
    }

    [Fact]
    public void SlotAt_AboveTheBoard_ClampsToTheFirstRow() {
        Assert.Equal(0, WidgetBoardLayout.SlotAt(Grid(), GridRows, 10, -400));
    }

    [Fact]
    public void SlotAt_BelowTheBoard_ClampsToTheLastRow() {
        Assert.Equal(5, WidgetBoardLayout.SlotAt(Grid(), GridRows, 900, 5000));
    }

    // A one-column strip: three 200x60 slots stacked with a 16px gutter, the shape of the
    // Performance device rail.
    private static Rect2[] Column() => new[] {
        new Rect2(0, 0, 200, 60), new Rect2(0, 76, 200, 60), new Rect2(0, 152, 200, 60),
    };

    private static readonly int[] ColumnRows = { 1, 2, 3 };

    /// <summary>A one-column strip is rows all the way down, so y alone decides — including below the
    /// last slot, which is how anything is ever dropped at the bottom of the rail.</summary>
    [Theory]
    [InlineData(10, 0)]
    [InlineData(50, 0)]
    [InlineData(90, 1)]
    [InlineData(160, 2)]
    [InlineData(200, 2)]
    [InlineData(5000, 2)]    // below the strip entirely
    public void SlotAt_InAColumn_TakesTheSlotTheDragCovers(double y, int expected) {
        Assert.Equal(expected, WidgetBoardLayout.SlotAt(Column(), ColumnRows, 100, y));
    }

    /// <summary>And it says the same wherever along the row the drag is held, which is what stops the
    /// grip's own position from deciding the answer.</summary>
    [Theory]
    [InlineData(5)]
    [InlineData(100)]
    [InlineData(195)]
    public void SlotAt_InAColumn_IgnoresX(double x) {
        Assert.Equal(2, WidgetBoardLayout.SlotAt(Column(), ColumnRows, x, 200));
        Assert.Equal(0, WidgetBoardLayout.SlotAt(Column(), ColumnRows, x, 10));
    }

    [Fact]
    public void SlotAt_Empty_IsZero() {
        Assert.Equal(0, WidgetBoardLayout.SlotAt(Array.Empty<Rect2>(), Array.Empty<int>(), 10, 10));
    }

    [Theory]
    [InlineData(0.5, 0.5, 350, 250)]   // grabbed dead centre
    [InlineData(0, 0, 500, 300)]       // grabbed at the top-left corner
    [InlineData(1, 1, 200, 200)]       // grabbed at the bottom-right corner
    public void DragRect_KeepsTheGripUnderThePointer(double grabX, double grabY,
                                                     double expectedLeft, double expectedTop) {
        var rect = WidgetBoardLayout.DragRect(300, 100, 500, 300, grabX, grabY);

        Assert.Equal(expectedLeft, rect.Left, 6);
        Assert.Equal(expectedTop, rect.Top, 6);
        Assert.Equal(300, rect.Width);
        Assert.Equal(100, rect.Height);
    }

    /// <summary>The grip is a fraction, so a widget picked up by its right edge stays under the
    /// pointer even once the slot it came from is a different size.</summary>
    [Fact]
    public void DragRect_ScalesTheGripWithTheWidth() {
        var wide = WidgetBoardLayout.DragRect(760, 100, 500, 300, 0.9, 0.5);
        var narrow = WidgetBoardLayout.DragRect(340, 100, 500, 300, 0.9, 0.5);

        Assert.Equal(500 - 0.9 * 760, wide.Left, 6);
        Assert.Equal(500 - 0.9 * 340, narrow.Left, 6);
    }

    /// <summary>Whether the row has more content width than its caps can between them absorb, which
    /// is the point at which the caps give way rather than banking the rest as whitespace.</summary>
    private static bool ExceedsEveryCap(ReadOnlySpan<WidgetSlot> row, double width) {
        var caps = 0.0;
        foreach (var slot in row)
            caps += Math.Max(slot.MinWidth, slot.MaxWidth);
        return width - Gutter * (row.Length - 1) > caps;
    }
}
