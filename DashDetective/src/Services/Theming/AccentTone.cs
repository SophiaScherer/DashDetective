using Avalonia.Media;
using System;

namespace DashDetective.Services.Theming;

/// <summary>
/// The rule every accent shade follows: an accent is one authored identity colour, which <i>is</i> its
/// dark fill, and every other shade is that identity re-lightened to a target CIE L*. Hue and saturation
/// never change, so an accent cannot shift identity between themes.
///
/// The rungs are Blue's measured lightness, so the default accent reproduces exactly. Pure colour maths
/// over <c>Avalonia.Media</c> value types, so it is unit-testable like <see cref="ChartPalette"/>.
/// </summary>
internal static class AccentTone {
    // ----- The ladder: one set of rungs, shared by every accent -----

    /// <summary>Every authored identity sits on this rung; <c>AccentIdentityTests</c> fails if one drifts
    /// off it.</summary>
    internal const double DarkFill = 74.4;

    internal const double DarkHover = 79.1;

    internal const double DarkDeep = 52.3;

    /// <summary>Dark's on-accent text. Outside the hue family — see <see cref="OnAccent"/>.</summary>
    internal const double DarkOnAccent = 14.0;

    internal const double LightFill = 48.0;

    internal const double LightHover = 42.2;

    internal const double LightDeep = 30.5;

    /// <summary>One accent's four shades for the theme being rendered.</summary>
    internal static AccentShades Shades(Color identity, bool dark) => dark
        ? new AccentShades(identity,
                           WithLightness(identity, DarkHover),
                           OnAccent(identity, dark: true),
                           WithLightness(identity, DarkDeep))
        : new AccentShades(WithLightness(identity, LightFill),
                           WithLightness(identity, LightHover),
                           OnAccent(identity, dark: false),
                           WithLightness(identity, LightDeep));

    /// <summary>Text drawn <i>on</i> the fill: read against it rather than beside it, so it is chosen for
    /// contrast rather than identity.</summary>
    internal static Color OnAccent(Color identity, bool dark) =>
        dark ? WithLightness(identity, DarkOnAccent) : Colors.White;

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
