using Avalonia.Media;
using System;

namespace DashDetective.Services.Theming;

/// <summary>
/// The rule every accent shade follows: an accent is one authored identity colour, which <i>is</i> its
/// fill, and every other shade is that identity re-lightened to a target CIE L*. Hue and saturation never
/// change, and the graphic shades do not change with the theme either.
///
/// The accent drawn as <b>text</b> is the one exception, darkened by the light theme because the identity
/// reads 2.01:1 on white against a 4.5:1 bar. Pure colour maths over <c>Avalonia.Media</c> value types, so
/// it is unit-testable like <see cref="ChartPalette"/>.
/// </summary>
internal static class AccentTone {
    // ----- The ladder: one set of rungs, shared by every accent -----

    /// <summary>Every authored identity sits on this rung; <c>AccentIdentityTests</c> fails if one drifts
    /// off it.</summary>
    internal const double Fill = 74.4;

    internal const double Hover = 79.1;

    internal const double Deep = 52.3;

    /// <summary>Text drawn <i>on</i> the fill: read against it rather than beside it, so it is chosen for
    /// contrast rather than identity.</summary>
    internal const double OnAccent = 14.0;

    /// <summary>The accent as text on a light page — the one rung a theme changes.</summary>
    internal const double LightText = 48.0;

    internal const double LightTextHover = 42.2;

    /// <summary>The graphic shades: logo, highlight bar, borders, buttons, swatches. One set for both
    /// themes — a brand colour that changed with the theme would not read as the same accent.</summary>
    internal static AccentShades Graphic(Color identity) =>
        new(identity,
            WithLightness(identity, Hover),
            WithLightness(identity, OnAccent),
            WithLightness(identity, Deep));

    /// <summary>The accent as text on the page background, for the theme being rendered.</summary>
    internal static AccentTextShades Text(Color identity, bool dark) => dark
        ? new AccentTextShades(identity, WithLightness(identity, Hover))
        : new AccentTextShades(WithLightness(identity, LightText),
                               WithLightness(identity, LightTextHover));

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

    /// <summary><paramref name="color"/>'s CIE L*. HSL lightness will not do: at one HSL value a
    /// saturated blue and a muted green read as different weights.</summary>
    internal static double Lightness(Color color) {
        var y = Luminance(color);
        return y > 0.008856 ? 116 * Math.Cbrt(y) - 16 : 903.3 * y;
    }

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
