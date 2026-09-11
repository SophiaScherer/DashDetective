using Avalonia.Media;

namespace DashDetective.Services.Theming;

/// <summary>
/// The rule every accent shade follows: an accent is one authored identity colour, which <i>is</i> its
/// fill, and every other shade is that identity re-lightened to a target CIE L*. Hue and saturation never
/// change, and the graphic shades do not change with the theme either.
///
/// The accent drawn as <b>text</b> is the one exception, darkened by the light theme because the identity
/// reads 2.01:1 on white against a 4.5:1 bar. The lightness maths itself lives on <see cref="Tone"/>,
/// which the text ramp and the chart series share; what is accent-specific is the rungs below.
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

    /// <summary>The step below <see cref="Tone.LightText"/>, which is where the accent's light text sits.
    /// Only the accent has a pointer-over state, so only this half of the pair is accent-specific.</summary>
    internal const double LightTextHover = 42.2;

    /// <summary>The graphic shades: logo, highlight bar, borders, buttons, swatches. One set for both
    /// themes — a brand colour that changed with the theme would not read as the same accent.</summary>
    internal static AccentShades Graphic(Color identity) =>
        new(identity,
            Tone.WithLightness(identity, Hover),
            Tone.WithLightness(identity, OnAccent),
            Tone.WithLightness(identity, Deep));

    /// <summary>The accent as text on the page background, for the theme being rendered.</summary>
    internal static AccentTextShades Text(Color identity, bool dark) => dark
        ? new AccentTextShades(identity, Tone.WithLightness(identity, Hover))
        : new AccentTextShades(Tone.WithLightness(identity, Tone.LightText),
                               Tone.WithLightness(identity, LightTextHover));
}
