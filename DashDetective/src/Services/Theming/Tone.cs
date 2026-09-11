using Avalonia.Media;
using System;

namespace DashDetective.Services.Theming;

/// <summary>
/// Perceived lightness, and how to move a colour to a target without changing what colour it is. The
/// shared half of the rule <see cref="AccentTone"/> introduced for the accent: hue and saturation are the
/// identity, CIE L* is the only thing a theme varies.
///
/// It is here rather than on <see cref="AccentTone"/> because the accent is no longer the only caller —
/// the light text ramp and the chart series' text shades are laid out on the same scale.
///
/// Pure colour maths over <c>Avalonia.Media</c> value types, so it is unit-testable without a render
/// backend.
/// </summary>
internal static class Tone {
    /// <summary><paramref name="color"/>'s CIE L*. HSL lightness will not do: at one HSL value a
    /// saturated blue and a muted green read as different weights.</summary>
    internal static double Lightness(Color color) {
        var y = Luminance(color);
        return y > 0.008856 ? 116 * Math.Cbrt(y) - 16 : 903.3 * y;
    }

    /// <summary><paramref name="identity"/> at <paramref name="target"/> CIE L*, keeping its hue,
    /// saturation and alpha. Bisected because L* has no closed form through the sRGB transfer curve.</summary>
    internal static Color WithLightness(Color identity, double target) {
        // The HSL round-trip is lossy by an 8-bit step, and half an L* is finer than a channel can
        // express, so a colour asked for its own lightness is handed straight back.
        if (Math.Abs(Lightness(identity) - target) < 0.5)
            return identity;

        var hsl = identity.ToHsl();
        double low = 0, high = 1;

        // 24 halvings take the interval well below one 8-bit step.
        for (var i = 0; i < 24; i++) {
            var mid = (low + high) / 2;
            if (Lightness(At(hsl, mid)) < target)
                low = mid;
            else
                high = mid;
        }

        return At(hsl, (low + high) / 2);
    }

    /// <summary>The L* of <paramref name="foreground"/> drawn at <paramref name="alpha"/> over
    /// <paramref name="background"/>. The text ramp is opacity over a surface rather than a colour, so a
    /// rung's weight is only meaningful once composited.</summary>
    internal static double CompositedLightness(Color foreground, double alpha, Color background) =>
        Lightness(Composite(foreground, alpha, background));

    /// <summary>Source-over compositing in sRGB space, which is what the renderer does for an opacity on
    /// a solid brush.</summary>
    private static Color Composite(Color fore, double alpha, Color back) => Color.FromRgb(
        Channel(fore.R, alpha, back.R), Channel(fore.G, alpha, back.G), Channel(fore.B, alpha, back.B));

    private static byte Channel(byte fore, double alpha, byte back) =>
        (byte)Math.Round(fore * alpha + back * (1 - alpha));

    /// <summary>An HSL colour at a different lightness, back as RGB.</summary>
    private static Color At(HslColor hsl, double lightness) =>
        HslColor.FromAhsl(hsl.A, hsl.H, hsl.S, lightness).ToRgb();

    /// <summary>WCAG relative luminance, matching what the contrast tests measure.</summary>
    private static double Luminance(Color c) =>
        0.2126 * Linearise(c.R) + 0.7152 * Linearise(c.G) + 0.0722 * Linearise(c.B);

    private static double Linearise(byte channel) {
        var v = channel / 255.0;
        return v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
    }
}
