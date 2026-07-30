using System;

namespace DashDetective.Shared.Layout;

/// <summary>
/// Split arithmetic for <c>WeightedRowPanel</c>: whether a weighted row still clears every
/// child's minimum width, and how the row divides when it does. Kept separate from the panel so it
/// is testable without a layout pass.
/// </summary>
public static class WeightedRowLayout {
    /// <summary>The narrowest content width (gutters already removed) at which every slice of the
    /// weighted split still meets its child's minimum. Below this the caller stacks instead. Driven
    /// by the child with the worst minimum-to-weight ratio; zero when nothing has a minimum.</summary>
    public static double RequiredWidth(ReadOnlySpan<double> weights, ReadOnlySpan<double> minWidths) {
        var total = 0.0;
        var worst = 0.0;
        for (var i = 0; i < weights.Length; i++) {
            var weight = weights[i];
            if (weight <= 0)
                continue; // a zero-weight child gets no slice, so it cannot set the requirement
            total += weight;
            var needed = minWidths[i] / weight;
            if (needed > worst)
                worst = needed;
        }

        return total * worst;
    }

    /// <summary>Divides <paramref name="contentWidth"/> across <paramref name="weights"/> in
    /// proportion, into <paramref name="widths"/>. Any rounding remainder goes to the last positive
    /// slice so the slices sum exactly to <paramref name="contentWidth"/> and leave no seam. Weights
    /// that are all zero divide the row evenly.</summary>
    public static void Split(double contentWidth, ReadOnlySpan<double> weights, Span<double> widths) {
        var total = 0.0;
        foreach (var weight in weights)
            if (weight > 0)
                total += weight;

        if (total <= 0) {
            var even = weights.Length > 0 ? contentWidth / weights.Length : 0;
            for (var i = 0; i < widths.Length; i++)
                widths[i] = even;
            return;
        }

        var last = -1;
        var used = 0.0;
        for (var i = 0; i < weights.Length; i++) {
            if (weights[i] <= 0) {
                widths[i] = 0;
                continue;
            }
            widths[i] = contentWidth * weights[i] / total;
            used += widths[i];
            last = i;
        }

        if (last >= 0)
            widths[last] += contentWidth - used;
    }
}
