using Avalonia.Media;
using System.Linq;

namespace DashDetective.Services.Theming;

/// <summary>
/// Whether an accent's fill can still be seen on a theme's surfaces. Text is corrected by
/// <see cref="AccentTone"/>; the fill cannot be without no longer being the chosen colour, so it is flagged.
/// </summary>
internal static class AccentGuard {
    /// <summary>The weakest a fill may read against a surface. The default's worst is 1.76:1, Blue on the
    /// light app background.</summary>
    internal const double VisibleFloor = 1.5;

    /// <summary>Mirrors the Dark surfaces in Palette.axaml: app, sidebar, panel, card, field.
    /// <c>AccentGuardTests</c> pins them.</summary>
    internal static readonly Color[] DarkSurfaces = [
        Color.Parse("#141414"), Color.Parse("#1b1b1b"), Color.Parse("#1c1c1c"), Color.Parse("#212121"),
        Color.Parse("#141414"),
    ];

    /// <summary>Mirrors the Light surfaces, in the same order.</summary>
    internal static readonly Color[] LightSurfaces = [
        Color.Parse("#eef0f2"), Color.Parse("#f7f8fa"), Color.Parse("#ffffff"), Color.Parse("#ffffff"),
        Color.Parse("#eceef1"),
    ];

    /// <summary>The fill's weakest contrast against a theme's surfaces.</summary>
    internal static double WorstContrast(Color fill, bool dark) =>
        (dark ? DarkSurfaces : LightSurfaces).Min(surface => Tone.Contrast(fill, surface));

    /// <summary>Whether the fill falls under <see cref="VisibleFloor"/> somewhere on that theme.</summary>
    internal static bool IsFaint(Color fill, bool dark) => WorstContrast(fill, dark) < VisibleFloor;
}
