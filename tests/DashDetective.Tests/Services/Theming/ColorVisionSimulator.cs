using Avalonia.Media;
using System;

namespace DashDetective.Tests.Services.Theming;

/// <summary>
/// Simulates dichromatic vision, so a colour-blind-safe palette can be checked rather than asserted.
///
/// Viénot, Brettel and Mollon (1999): convert to long/medium/short cone response, collapse the missing
/// cone onto the plane the remaining two span, and convert back. The projection planes are the standard
/// ones, anchored on the blue and yellow the dichromat still sees correctly.
///
/// It is an approximation of a *dichromat* — the complete forms. Someone with the far commoner anomalous
/// trichromacy sees more than this, so a palette that survives here survives the milder case too, which
/// is the direction an accessibility check should err in.
/// </summary>
internal static class ColorVisionSimulator {
    /// <summary>
    /// How far apart two colours must stay under simulation. CIE76 ΔE of 2.3 is the "just noticeable
    /// difference"; this asks for roughly nine times that, because these are small marks — a dot, a
    /// two-pixel line — seen at a glance rather than large patches compared side by side.
    ///
    /// <b>20 is where the light theme runs out, not a number picked for comfort.</b> Searching every
    /// candidate that clears 3:1 on white, the best five mutually distinguishable colours separate by
    /// about 21.5 under red-green deficiency; the dark theme reaches 35-50 in the same search. The bar is
    /// set just under what the harder of the two themes can actually achieve, so raising it means finding
    /// colours nobody has found yet rather than relaxing the palettes.
    /// </summary>
    internal const double MinimumSeparation = 20.0;

    /// <summary>A colour as a dichromat of the given kind would see it.</summary>
    internal static Color Simulate(Color color, string kind) {
        var (r, g, b) = (Linear(color.R), Linear(color.G), Linear(color.B));

        // Hunt-Pointer-Estevez, normalised to D65.
        var l = 0.31399022 * r + 0.63951294 * g + 0.04649755 * b;
        var m = 0.15537241 * r + 0.75789446 * g + 0.08670142 * b;
        var s = 0.01775239 * r + 0.10944209 * g + 0.87256922 * b;

        (l, m, s) = kind switch {
            "protan" => (1.05118294 * m - 0.05116099 * s, m, s),
            "deutan" => (l, 0.9513092 * l + 0.04866992 * s, s),
            _ => (l, m, -0.86744736 * l + 1.86727089 * m), // tritan
        };

        var rr = 5.47221206 * l - 4.6419601 * m + 0.16963708 * s;
        var gg = -1.1252419 * l + 2.29317094 * m - 0.1678952 * s;
        var bb = 0.02980165 * l - 0.19318073 * m + 1.16364789 * s;

        return Color.FromRgb(Srgb(rr), Srgb(gg), Srgb(bb));
    }

    /// <summary>CIE76 colour difference in Lab, which is close enough to perceptual for a "are these two
    /// still obviously different" check.</summary>
    internal static double Difference(Color a, Color b) {
        var (l1, a1, b1) = Lab(a);
        var (l2, a2, b2) = Lab(b);
        return Math.Sqrt((l1 - l2) * (l1 - l2) + (a1 - a2) * (a1 - a2) + (b1 - b2) * (b1 - b2));
    }

    /// <summary>The two colours' separation once both are seen through the deficiency.</summary>
    internal static double SeparationUnder(Color a, Color b, string kind) =>
        Difference(Simulate(a, kind), Simulate(b, kind));

    private static double Linear(byte channel) {
        var v = channel / 255.0;
        return v <= 0.04045 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
    }

    private static byte Srgb(double linear) {
        var v = linear <= 0.0031308 ? linear * 12.92 : 1.055 * Math.Pow(Math.Max(linear, 0), 1 / 2.4) - 0.055;
        return (byte)Math.Clamp(Math.Round(v * 255), 0, 255);
    }

    private static (double L, double A, double B) Lab(Color c) {
        var (r, g, b) = (Linear(c.R), Linear(c.G), Linear(c.B));

        // sRGB to XYZ (D65), then normalised by the white point.
        var x = (0.4124564 * r + 0.3575761 * g + 0.1804375 * b) / 0.95047;
        var y = 0.2126729 * r + 0.7151522 * g + 0.0721750 * b;
        var z = (0.0193339 * r + 0.1191920 * g + 0.9503041 * b) / 1.08883;

        double F(double t) => t > 0.008856 ? Math.Cbrt(t) : 7.787 * t + 16.0 / 116;

        return (116 * F(y) - 16, 500 * (F(x) - F(y)), 200 * (F(y) - F(z)));
    }
}
