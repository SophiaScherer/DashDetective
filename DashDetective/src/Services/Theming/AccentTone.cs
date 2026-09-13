using Avalonia.Media;
using System;

namespace DashDetective.Services.Theming;

/// <summary>
/// The rule every accent shade follows: an accent is one identity colour, which <i>is</i> its fill, and
/// every other shade is that identity re-lightened. Hue and saturation never change, and the graphic
/// shades do not change with the theme either.
///
/// The presets sit on the ladder's <see cref="Fill"/> rung and take its rungs exactly. A custom accent
/// keeps its own lightness, so each shade keeps the same distance from it instead, and anything drawn as
/// text is moved to a readable rung. The lightness maths itself lives on <see cref="Tone"/>.
/// </summary>
internal static class AccentTone {
    // ----- The ladder: one set of rungs, shared by every preset -----

    /// <summary>Every preset identity sits on this rung; <c>AccentIdentityTests</c> fails if one drifts
    /// off it.</summary>
    internal const double Fill = 74.4;

    internal const double Hover = 79.1;

    internal const double Deep = 52.3;

    /// <summary>Text drawn <i>on</i> the fill: read against it rather than beside it, so it is chosen for
    /// contrast rather than identity.</summary>
    internal const double OnAccent = 14.0;

    /// <summary>On-accent text for a fill too dark to carry <see cref="OnAccent"/>.</summary>
    internal const double OnAccentLight = 98.0;

    /// <summary>The step below <see cref="Tone.LightText"/>, which is where the accent's light text sits.
    /// Only the accent has a pointer-over state, so only this half of the pair is accent-specific.</summary>
    internal const double LightTextHover = 42.2;

    /// <summary>Where a dark custom accent is lifted to as text on a dark page: 4.75:1 on the lightest
    /// dark surface. Every preset already sits above it.</summary>
    internal const double DarkText = 58.0;

    /// <summary>Below this the logo gradient's deep stop turns lighter instead, or it vanishes into black.</summary>
    internal const double DeepFloor = 10.0;

    private const double AA = 4.5;

    /// <summary>The graphic shades: logo, highlight bar, borders, buttons, swatches. One set for both
    /// themes — a brand colour that changed with the theme would not read as the same accent.</summary>
    internal static AccentShades Graphic(Color identity) {
        var lightness = Tone.Lightness(identity);
        var onAccent = OnAccentFor(identity);

        // Hover moves away from the on-accent text, so the label only gains contrast.
        var hover = Tone.Lightness(onAccent) < lightness
            ? Step(lightness, Hover)
            : lightness - (Hover - Fill);

        var deep = Step(lightness, Deep);
        if (deep < DeepFloor)
            deep = lightness + (Fill - Deep);

        return new AccentShades(identity, At(identity, Reachable(lightness, hover)), onAccent, At(identity, deep));
    }

    /// <summary>The accent as text on the page background, for the theme being rendered.</summary>
    internal static AccentTextShades Text(Color identity, bool dark) {
        var lightness = Tone.Lightness(identity);

        if (dark) {
            var fill = lightness >= DarkText ? identity : Tone.WithLightness(identity, DarkText);
            var fillLightness = Tone.Lightness(fill);
            return new AccentTextShades(fill, At(identity, Reachable(fillLightness, Step(fillLightness, Hover))));
        }

        var hover = lightness > Tone.LightText
            ? LightTextHover
            : lightness - (Tone.LightText - LightTextHover);
        return new AccentTextShades(Tone.TextOnLight(identity), At(identity, Reachable(lightness, hover)));
    }

    /// <summary>The dark rung, else the near-white one, else whichever of black or white reads better —
    /// one of the two always clears AA.</summary>
    private static Color OnAccentFor(Color identity) {
        foreach (var rung in (ReadOnlySpan<double>)[OnAccent, OnAccentLight]) {
            var candidate = Tone.WithLightness(identity, rung);
            if (Tone.Contrast(candidate, identity) >= AA)
                return candidate;
        }

        return Tone.Contrast(Colors.Black, identity) >= Tone.Contrast(Colors.White, identity)
            ? Colors.Black
            : Colors.White;
    }

    /// <summary>A preset's rung, or the same distance from a custom identity's own lightness. Within half
    /// an L* counts as on the rung, which keeps the presets byte-identical.</summary>
    private static double Step(double lightness, double rung) =>
        Math.Abs(lightness - Fill) < 0.5 ? rung : lightness + (rung - Fill);

    /// <summary>A hover step past black or white is taken the other way, or it clamps back onto the fill
    /// and the pointer-over state disappears. At either end the label's contrast is far above AA.</summary>
    private static double Reachable(double from, double target) =>
        target is < 0 or > 100 ? 2 * from - target : target;

    private static Color At(Color identity, double lightness) =>
        Tone.WithLightness(identity, Math.Clamp(lightness, 0, 100));
}
