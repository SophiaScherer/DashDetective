using System;

namespace DashDetective.Shared.Controls;

/// <summary>
/// How far one wheel notch moves a scroll surface. Avalonia hardcodes 50 px and offers no property for
/// it, so the distance comes from the OS's lines-per-notch setting instead.
/// </summary>
internal static class WheelStep {
    /// <summary>What Windows ships with, and what an unreadable setting falls back to.</summary>
    internal const int DefaultLines = 3;

    /// <summary>The OS value for "one screen at a time".</summary>
    internal const int PageLines = -1;

    /// <summary>One line in pixels: the app's table row, which is about what Explorer steps by.</summary>
    internal const double LineHeight = 30;

    /// <summary>Left on screen when a notch moves a whole page.</summary>
    private const double PageOverlap = LineHeight;

    /// <summary>Offset units one notch moves. <paramref name="unit"/> is a line in the surface's own
    /// units — pixels, or one item where it scrolls by item.</summary>
    internal static double Distance(int? lines, double viewport, double unit) {
        var n = lines ?? DefaultLines;

        if (n == PageLines)
            return Math.Max(viewport - PageOverlap, unit);

        // 0 is the OS's "do not scroll" and is honored. No other non-positive value means anything, and a
        // frozen wheel looks like a broken app, so those take the default.
        if (n == 0)
            return 0;

        return n < 0 ? DefaultLines * unit : n * unit;
    }
}
