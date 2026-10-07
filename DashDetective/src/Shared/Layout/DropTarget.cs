using System;

namespace DashDetective.Shared.Layout;

/// <summary>
/// Which slot a drag is over, decided against the slots as they were laid out when it began.
///
/// Reading the live layout fed the preview back into its own input: moving the dragged item re-packs
/// the slots under a still pointer, which could flip the answer back, which re-packed them again — a
/// widget held between a narrow and a wide neighbor flickered between the two. The snapshot cannot
/// move, and <see cref="Hysteresis"/> keeps a pointer jittering on a boundary from flipping it either.
/// No Avalonia types, so it tests without a layout pass.
/// </summary>
public sealed class DropTarget {
    /// <summary>How far past a boundary the drag must go before the target changes, as a fraction of
    /// the smaller of the two slots on that axis. Relative, so it scales with the interface size, and
    /// under a half, so every slot can still be reached.</summary>
    public const double Hysteresis = 0.2;

    private Rect2[] _slots = Array.Empty<Rect2>();
    private int[] _rowEnds = Array.Empty<int>();

    /// <summary>The slot the drag currently takes, or -1 before <see cref="Begin"/>.</summary>
    public int Current { get; private set; } = -1;

    /// <summary>How many slots the snapshot holds, so a caller can tell it has gone stale.</summary>
    public int SlotCount => _slots.Length;

    /// <summary>Freezes the settled slots and starts from the slot the item was picked up from.</summary>
    public void Begin(ReadOnlySpan<Rect2> slots, ReadOnlySpan<int> rowEnds, int start) {
        _slots = slots.ToArray();
        _rowEnds = rowEnds.ToArray();
        Current = start;
    }

    /// <summary>The slot a drag whose box is centered here takes now.</summary>
    public int Update(double x, double y) {
        Current = Resolve(_slots, _rowEnds, x, y, Current);
        return Current;
    }

    /// <summary>The slot under (<paramref name="x"/>, <paramref name="y"/>) by
    /// <see cref="WidgetBoardLayout.SlotAt"/>, except that <paramref name="current"/> is kept until the
    /// point is a margin clear of the region it owns.</summary>
    public static int Resolve(ReadOnlySpan<Rect2> slots, ReadOnlySpan<int> rowEnds, double x, double y,
                              int current) {
        var under = WidgetBoardLayout.SlotAt(slots, rowEnds, x, y);
        if (under == current || current < 0 || current >= slots.Length)
            return under;

        return Holds(slots, rowEnds, current, under, x, y) ? current : under;
    }

    // The region SlotAt gives to current, grown by the margin: its row's band, and from its own left
    // edge to the next slot's. A row's first and last slots run on to infinity, as SlotAt's clamps do.
    private static bool Holds(ReadOnlySpan<Rect2> slots, ReadOnlySpan<int> rowEnds, int current, int under,
                              double x, double y) {
        var row = RowOf(rowEnds, current);
        if (row < 0)
            return false;

        var start = row == 0 ? 0 : rowEnds[row - 1];
        var end = rowEnds[row];
        var held = slots[current];
        var next = slots[under];

        var marginX = Hysteresis * Math.Min(held.Width, next.Width);
        var marginY = Hysteresis * Math.Min(held.Height, next.Height);

        var left = current == start ? double.NegativeInfinity : held.Left;
        var right = current == end - 1 ? double.PositiveInfinity : slots[current + 1].Left;
        var top = row == 0 ? double.NegativeInfinity : slots[start - 1].Bottom;
        var bottom = row == rowEnds.Length - 1 ? double.PositiveInfinity : slots[end - 1].Bottom;

        return x >= left - marginX && x < right + marginX
               && y >= top - marginY && y < bottom + marginY;
    }

    private static int RowOf(ReadOnlySpan<int> rowEnds, int index) {
        for (var r = 0; r < rowEnds.Length; r++)
            if (index < rowEnds[r])
                return r;
        return -1;
    }
}
