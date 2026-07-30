using System;

namespace DashDetective.Shared.Layout;

/// <summary>
/// Column arithmetic for <c>UniformFlowPanel</c>: how many equal columns of a given minimum width
/// fit a row, and how wide each one ends up. Kept separate from the panel so it is testable without
/// a layout pass.
/// </summary>
public static class FlowLayout {
    // Available widths arrive from layout arithmetic (padding and gutters subtracted), so an exact
    // fit can land a hair under the integer. Nudge it back before flooring; a sub-pixel shortfall
    // is not a real reason to drop a column.
    private const double Epsilon = 1e-6;

    /// <summary>Columns of at least <paramref name="minItemWidth"/> that fit
    /// <paramref name="availableWidth"/> with <paramref name="spacing"/> between them. Never fewer
    /// than one, never more than <paramref name="itemCount"/>, and capped by
    /// <paramref name="maxColumns"/> when that is positive. An infinite width (an Auto slot) yields
    /// the uncapped maximum, since nothing forces a wrap there.</summary>
    public static int ColumnCount(double availableWidth, double minItemWidth, double spacing,
                                  int itemCount, int maxColumns = 0) {
        var ceiling = maxColumns > 0 ? Math.Min(itemCount, maxColumns) : itemCount;
        if (ceiling <= 1)
            return Math.Max(1, ceiling);

        // n columns fit when n·min + (n−1)·spacing ≤ available.
        if (minItemWidth > 0 && double.IsFinite(availableWidth)) {
            var fit = (int)Math.Floor((availableWidth + spacing) / (minItemWidth + spacing) + Epsilon);
            return Math.Clamp(fit, 1, ceiling);
        }

        return ceiling;
    }

    /// <summary>The width each of <paramref name="columns"/> equal columns gets once the
    /// <c>columns − 1</c> gutters are removed. Never negative.</summary>
    public static double ItemWidth(double availableWidth, int columns, double spacing) {
        if (columns < 1 || !double.IsFinite(availableWidth))
            return 0;

        var content = availableWidth - spacing * (columns - 1);
        return content > 0 ? content / columns : 0;
    }
}
