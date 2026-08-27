using System;
using System.Collections.Generic;

namespace DashDetective.Shared.Layout;

/// <summary>One widget's inputs to the board's arithmetic.</summary>
/// <param name="Weight">Share of its row. Row-local, so it changes meaning when the row does.</param>
/// <param name="MinWidth">Below this the widget stops being readable.</param>
/// <param name="MaxWidth">Above this it stops being readable the other way. Infinity for no cap.</param>
/// <param name="BreakBefore">Start a new row here even where the previous one had room.</param>
/// <param name="Stretch">This widget owns its row (a card strip, a full-width table).</param>
/// <summary>An arranged widget's box, declared here so the drop arithmetic stays Avalonia-free.</summary>
public readonly record struct Rect2(double Left, double Top, double Width, double Height) {
    public double Bottom => Top + Height;
}

public readonly record struct WidgetSlot(
    double Weight,
    double MinWidth,
    double MaxWidth,
    bool BreakBefore = false,
    bool Stretch = false);

/// <summary>
/// Which widgets share a row, and how wide each one is. A row keeps pulling the next widget in while
/// it is still too roomy, so surplus width buys a column rather than a wider widget.
///
/// Packs greedily in declared order (a dragged widget has to land where the user put it), defers each
/// split to <see cref="WeightedRowLayout"/>, and holds no Avalonia types so it tests without layout.
/// </summary>
public static class WidgetBoardLayout {
    // An exact fit can land a hair under after gutters are subtracted; the nudge FlowLayout also uses.
    private const double Epsilon = 1e-6;

    /// <summary>The index just past each row: row <c>r</c> holds slots
    /// <c>rowEnds[r-1]..rowEnds[r]-1</c>. Every slot lands in exactly one row, in declared order.</summary>
    public static List<int> PackRows(ReadOnlySpan<WidgetSlot> slots, double availableWidth,
                                     double columnSpacing) {
        var rowEnds = new List<int>();
        if (slots.Length == 0)
            return rowEnds;

        var start = 0;
        while (start < slots.Length) {
            var end = start + 1;

            // A stretching widget owns its row, so it never takes companions.
            if (!slots[start].Stretch) {
                while (end < slots.Length
                       && !slots[end].BreakBefore
                       && !slots[end].Stretch
                       && HasSurplus(slots[start..end], availableWidth, columnSpacing)
                       && Fits(slots[start..(end + 1)], availableWidth, columnSpacing))
                    end++;
            }

            rowEnds.Add(end);
            start = end;
        }

        MergeTrailingOrphan(slots, availableWidth, columnSpacing, rowEnds);
        return rowEnds;
    }

    /// <summary>Pulls a lone last widget up into the row above when it still clears every minimum
    /// there, so a page does not end on one panel spanning the width beside a capped neighbour.</summary>
    private static void MergeTrailingOrphan(ReadOnlySpan<WidgetSlot> slots, double availableWidth,
                                            double columnSpacing, List<int> rowEnds) {
        if (rowEnds.Count < 2)
            return;

        var lastStart = rowEnds[^2];
        if (slots.Length - lastStart != 1 || slots[lastStart].Stretch || slots[lastStart].BreakBefore)
            return;

        // Only where the whole merged row is capped: an uncapped widget owns its row on purpose.
        var previousStart = rowEnds.Count >= 3 ? rowEnds[^3] : 0;
        foreach (var slot in slots[previousStart..])
            if (slot.Stretch || double.IsPositiveInfinity(slot.MaxWidth))
                return;

        if (Fits(slots[previousStart..], availableWidth, columnSpacing))
            rowEnds.RemoveAt(rowEnds.Count - 2);
    }

    /// <summary>Whether this row has more width than its widgets can readably use, so another one
    /// should be pulled in rather than letting these grow past their caps. An unconstrained width is
    /// always surplus; a row holding anything uncapped never is.</summary>
    public static bool HasSurplus(ReadOnlySpan<WidgetSlot> slots, double availableWidth,
                                  double columnSpacing) {
        if (slots.Length == 0)
            return true;
        if (!double.IsFinite(availableWidth))
            return true;

        var caps = 0.0;
        foreach (var slot in slots) {
            if (slot.Stretch || double.IsPositiveInfinity(slot.MaxWidth))
                return false;
            caps += Math.Max(slot.MinWidth, slot.MaxWidth);
        }

        var content = availableWidth - columnSpacing * (slots.Length - 1);
        return content > caps + Epsilon;
    }

    /// <summary>Whether these slots can share one row at all: the weighted split has to leave every
    /// one of them at or above its own minimum.</summary>
    public static bool Fits(ReadOnlySpan<WidgetSlot> slots, double availableWidth, double columnSpacing) {
        if (slots.Length <= 1)
            return true;
        if (!double.IsFinite(availableWidth))
            return true;

        var content = availableWidth - columnSpacing * (slots.Length - 1);
        if (content <= 0)
            return false;

        Span<double> weights = stackalloc double[slots.Length];
        Span<double> minimums = stackalloc double[slots.Length];
        for (var i = 0; i < slots.Length; i++) {
            weights[i] = Math.Max(0, slots[i].Weight);
            minimums[i] = slots[i].MinWidth;
        }

        return content + Epsilon >= WeightedRowLayout.RequiredWidth(weights, minimums);
    }

    /// <summary>
    /// Divides one row across its slots: split by weight, clamp each to its own range, then share the
    /// width the clamped ones gave back among those still free, repeating until nothing new clamps.
    /// </summary>
    public static void SplitRow(ReadOnlySpan<WidgetSlot> slots, double availableWidth,
                                double columnSpacing, Span<double> widths) {
        var count = slots.Length;
        if (count == 0)
            return;

        var content = Math.Max(0, availableWidth - columnSpacing * (count - 1));

        if (count == 1) {
            widths[0] = content;
            return;
        }

        Span<double> weights = stackalloc double[count];
        for (var i = 0; i < count; i++)
            weights[i] = Math.Max(0, slots[i].Weight);

        Span<bool> clamped = stackalloc bool[count];
        var free = content;

        for (var pass = 0; pass <= count; pass++) {
            var freeWeight = 0.0;
            var freeCount = 0;
            for (var i = 0; i < count; i++) {
                if (clamped[i])
                    continue;
                freeWeight += weights[i];
                freeCount++;
            }

            if (freeCount == 0)
                break;

            var settled = true;
            for (var i = 0; i < count; i++) {
                if (clamped[i])
                    continue;

                var share = freeWeight > 0 ? free * weights[i] / freeWeight : free / freeCount;
                var cap = Cap(slots[i]);

                if (share > cap + Epsilon) {
                    widths[i] = cap;
                    clamped[i] = true;
                    free -= cap;
                    settled = false;
                } else if (share + Epsilon < slots[i].MinWidth) {
                    widths[i] = slots[i].MinWidth;
                    clamped[i] = true;
                    free -= slots[i].MinWidth;
                    settled = false;
                } else {
                    widths[i] = share;
                }
            }

            if (settled)
                break;
        }

        var used = 0.0;
        for (var i = 0; i < count; i++)
            used += widths[i];
        var leftover = content - used;

        // Too narrow for the minimums: overflow at them rather than driving a width negative.
        if (leftover < -Epsilon)
            return;

        // Caps are what a widget can readably use, not a ceiling: once every slot has hit one and
        // width remains, they give way. Banking it as a gutter would leave most of the page empty.
        if (leftover > Epsilon) {
            WeightedRowLayout.Split(content, weights, widths);
            return;
        }

        widths[count - 1] += leftover;
    }

    /// <summary>Where a widget dragged to (<paramref name="x"/>, <paramref name="y"/>) would be
    /// inserted: the row whose band holds the pointer, then the first slot whose midpoint is right of
    /// it.</summary>
    public static int DropIndex(ReadOnlySpan<Rect2> slots, ReadOnlySpan<int> rowEnds, double x, double y) {
        if (slots.Length == 0)
            return 0;

        var row = rowEnds.Length - 1;
        var start = 0;
        for (var r = 0; r < rowEnds.Length; r++) {
            var end = rowEnds[r];
            if (y < slots[end - 1].Bottom) {
                row = r;
                break;
            }
            start = end;
        }

        // start tracks the row before the one chosen when the loop ran to the end, so recompute it.
        start = row == 0 ? 0 : rowEnds[row - 1];
        var rowEnd = rowEnds[row];

        for (var i = start; i < rowEnd; i++)
            if (x < slots[i].Left + slots[i].Width / 2)
                return i;

        return rowEnd;
    }

    private static double Cap(WidgetSlot slot) =>
        slot.Stretch ? double.PositiveInfinity : Math.Max(slot.MinWidth, slot.MaxWidth);
}
