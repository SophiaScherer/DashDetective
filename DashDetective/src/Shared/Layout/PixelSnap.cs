using System;

namespace DashDetective.Shared.Layout;

/// <summary>
/// Snaps a slot to whole device pixels before a panel arranges a child in it. Avalonia's own layout
/// rounding rounds a child's origin to the nearest pixel and its size UP, separately, so a slot on a
/// fractional edge can end a pixel past its panel, and the nearest clipping ancestor (a UserControl,
/// an ItemsControl) then cuts the card's right or bottom border off. A slot whose edges are already
/// whole pixels passes through that rounding unchanged. No Avalonia types, so it tests without layout.
/// </summary>
public static class PixelSnap {
    // Kept off the length so the framework's round-up cannot add a pixel to a whole-pixel size through
    // a floating-point error; far below anything that renders.
    private const double Shave = 1e-4;

    /// <summary>
    /// The span along one axis whose two edges land on whole device pixels at
    /// <paramref name="scale"/>: each edge goes to its nearest pixel, so neighbors keep the gap
    /// between them and an edge on the panel's boundary stays on it.
    /// </summary>
    /// <param name="start">Where the slot starts, in device-independent units.</param>
    /// <param name="length">How long the slot is.</param>
    /// <param name="minimum">The child's own minimum, which the arrange would enforce anyway. Where
    /// snapping would land under it, the span grows to it away from <paramref name="limit"/>, rather
    /// than leaving the framework to center the overflow across both edges.</param>
    /// <param name="limit">The panel's extent along this axis; infinity for none.</param>
    /// <param name="scale">Device pixels per unit. Anything not positive leaves the span untouched,
    /// which is what a panel without layout rounding wants.</param>
    public static (double Start, double Length) Span(double start, double length, double minimum,
                                                     double limit, double scale) {
        if (!(scale > 0) || !double.IsFinite(scale) || !double.IsFinite(start) || !double.IsFinite(length))
            return (start, length);

        var near = Round(start * scale);
        var far = Math.Max(near, Round((start + length) * scale));
        var least = minimum > 0 && double.IsFinite(minimum) ? MinimumPixels(minimum, scale) : 0;

        if (far - near < least) {
            var edge = double.IsFinite(limit) ? Round(limit * scale) : double.PositiveInfinity;
            if (near + least <= edge || edge - least < 0) {
                far = near + least;
            } else {
                far = edge;
                near = edge - least;
            }
        }

        return (near / scale, Math.Max(0, far - near - Shave) / scale);
    }

    // Half up rather than to even, so moving a span by whole pixels never changes its length:
    // banker's rounding sends 1.5 and 2.5 both to 2.
    private static double Round(double pixels) => Math.Floor(pixels + 0.5);

    // The pixels the framework clamps a child to, counted its way: anything less and the clamp would
    // still add one, and the span would end past the panel.
    private static double MinimumPixels(double minimum, double scale) =>
        scale == 1
            ? Math.Ceiling(minimum)
            : Math.Ceiling(Math.Round(minimum, 8, MidpointRounding.ToZero) * scale);
}
