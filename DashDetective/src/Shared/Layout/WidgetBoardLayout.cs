using System;
using System.Collections.Generic;

namespace DashDetective.Shared.Layout;

/// <summary>One widget's inputs to the board's arithmetic.</summary>
/// <param name="Weight">Share of the row this widget takes. Row-local: it means "this much of
/// whatever shares my row", so it changes meaning when the row's membership does.</param>
/// <param name="MinWidth">The width below which the widget stops being readable.</param>
/// <param name="MaxWidth">The width above which it stops being readable the other way — a table
/// stretched across an ultrawide leaves its columns metres apart.
/// <see cref="double.PositiveInfinity"/> for no cap.</param>
/// <param name="BreakBefore">Start a new row at this widget even if the previous one had room.</param>
/// <param name="Stretch">This widget owns its row: a card strip or a wide table that is meant to span
/// the page rather than sit beside anything.</param>
public readonly record struct WidgetSlot(
    double Weight,
    double MinWidth,
    double MaxWidth,
    bool BreakBefore = false,
    bool Stretch = false);

/// <summary>
/// Row packing and width arithmetic for <see cref="WidgetBoard"/>. Answers the two questions the panel
/// then only has to arrange: which widgets share a row, and how wide each one is.
///
/// The cap is the point of the whole class: a wide window should buy more columns, not wider widgets.
/// Left to stretch, the Network tab's connections table runs past 2000px on an ultrawide with four
/// columns of whitespace between its headings. So a row keeps pulling the next widget in while it is
/// still too roomy — while the width it has exceeds what its members can readably use — and stops as
/// soon as they can. That is what turns surplus width into another column.
///
/// Rows pack greedily and in declared order, never reordered to pack better: the user drags widgets
/// into an order and has to be able to predict where one lands. Each row's proportional split is
/// <see cref="WeightedRowLayout"/>'s, so that arithmetic and its tests stay the one source of truth.
/// Kept free of Avalonia types so it is testable without a layout pass.
/// </summary>
public static class WidgetBoardLayout {
    // Widths arrive from layout arithmetic (padding and gutters already subtracted), so an exact fit
    // can land a hair under. The same nudge FlowLayout applies, for the same reason.
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

    /// <summary>Pulls a lone last widget back into the row above when it still clears every minimum
    /// there. Without this a page whose widget count does not divide evenly ends on one widget
    /// spanning the full width beside a capped neighbour above it, which reads as a mistake rather
    /// than a layout. Only the last row, and only one merge: anything more would start reordering
    /// what the packing deliberately kept in place.</summary>
    private static void MergeTrailingOrphan(ReadOnlySpan<WidgetSlot> slots, double availableWidth,
                                            double columnSpacing, List<int> rowEnds) {
        if (rowEnds.Count < 2)
            return;

        var lastStart = rowEnds[^2];
        if (slots.Length - lastStart != 1 || slots[lastStart].Stretch || slots[lastStart].BreakBefore)
            return;

        // Only where the whole merged row is capped. A widget that declares no ceiling was left to own
        // its row on purpose, and pulling the orphan up beside it would override that with a rule the
        // cap system never applied to it in the first place.
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
    /// Divides one row across its slots, into <paramref name="widths"/>.
    ///
    /// Split by weight, clamp each slot to its own range, then hand the width the clamped slots gave
    /// back to those still free, in proportion. Clamping one can push another over its cap, so this
    /// repeats until nothing new clamps — at most once per slot, since a clamped slot never unclamps.
    ///
    /// A cap is what a widget can readably USE, not a hard ceiling: once every slot has hit one and
    /// width is still left, the leftover is shared out in proportion anyway. By then the row is as
    /// full as it will get — <see cref="PackRows"/> already spent every chance to buy a column with
    /// that width — and half an empty screen is worse than a wide panel. This is the case a page with
    /// only two widgets hits on an ultrawide.
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

        // Too narrow to honour the minimums at all: overflow at them rather than driving a width
        // negative. The caller is inside a ScrollViewer, and a minimum is the point below which the
        // widget stopped being readable anyway.
        if (leftover < -Epsilon)
            return;

        // Every slot is capped and width still remains, so the caps give way entirely and the row is
        // just the weighted split. Banking the leftover as a gutter instead would put most of a 3000px
        // page into one gap, which is the unused whitespace the caps were introduced to get rid of.
        if (leftover > Epsilon) {
            WeightedRowLayout.Split(content, weights, widths);
            return;
        }

        widths[count - 1] += leftover;
    }

    private static double Cap(WidgetSlot slot) =>
        slot.Stretch ? double.PositiveInfinity : Math.Max(slot.MinWidth, slot.MaxWidth);
}
