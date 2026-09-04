using System;

namespace DashDetective.Tests.Services.Theming;

/// <summary>
/// WCAG relative luminance and contrast ratio, for the palette tests.
///
/// The one thing that makes this more than a formula lookup: <b>the text ramp is opacity over a
/// surface</b>, not a color. <c>TextMuted</c> is white at 50% over <c>#141414</c>, so compositing has
/// to happen before the ratio is taken — reading it as plain white would score every ramp entry 21:1
/// and the test would pass while proving nothing.
/// </summary>
internal static class ContrastRatio {
    /// <summary>WCAG 2 AA for body text.</summary>
    internal const double AA = 4.5;

    /// <summary>WCAG 2 AAA for body text, and what a high-contrast theme is for.</summary>
    internal const double AAA = 7.0;

    /// <summary>The ratio between a foreground drawn at <paramref name="alpha"/> over a background.
    /// Always ≥ 1, and ordered so the lighter color is the numerator.</summary>
    internal static double Of((int R, int G, int B) foreground, double alpha, (int R, int G, int B) background) {
        var composited = Composite(foreground, alpha, background);
        var a = Luminance(composited) + 0.05;
        var b = Luminance(background) + 0.05;
        return a > b ? a / b : b / a;
    }

    /// <summary>Source-over compositing in sRGB space, which is what the renderer does for an opacity on
    /// a solid brush.</summary>
    private static (int R, int G, int B) Composite((int R, int G, int B) fg, double alpha, (int R, int G, int B) bg) =>
        ((int)Math.Round(fg.R * alpha + bg.R * (1 - alpha)),
         (int)Math.Round(fg.G * alpha + bg.G * (1 - alpha)),
         (int)Math.Round(fg.B * alpha + bg.B * (1 - alpha)));

    /// <summary>WCAG relative luminance: linearise each channel, then weight by the eye's sensitivity.</summary>
    private static double Luminance((int R, int G, int B) c) =>
        0.2126 * Linearise(c.R) + 0.7152 * Linearise(c.G) + 0.0722 * Linearise(c.B);

    private static double Linearise(int channel) {
        var v = channel / 255.0;
        return v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
    }
}
