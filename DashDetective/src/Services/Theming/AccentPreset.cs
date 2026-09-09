using Avalonia.Media;
using System.Collections.Generic;

namespace DashDetective.Services.Theming;

/// <summary>
/// One accent's graphic shades: the fill, its pointer-over step, the color of text drawn on it, and the
/// brand gradient's bottom stop.
/// </summary>
public sealed record AccentShades(Color Fill, Color Hover, Color OnAccent, Color Deep);

/// <summary>The accent drawn as text on the page: the color and its pointer-over step.</summary>
public sealed record AccentTextShades(Color Fill, Color Hover);

/// <summary>
/// One selectable accent: a name and a single identity color, which is also its fill. Every shade comes
/// from <see cref="AccentTone"/>, so an accent keeps one appearance across themes.
///
/// Immutable; the fixed set lives in <see cref="All"/>. Applied by <see cref="ThemeService"/>.
/// </summary>
public sealed record AccentPreset(string Name, Color Identity) {
    /// <summary>The accent's identity hue, used to derive the chart palette and to fill the Settings
    /// swatch.</summary>
    public Color Color => Identity;

    /// <summary>The graphic shades. The same in both themes, which is what keeps the logo and the
    /// navigation highlight reading as one colour.</summary>
    public AccentShades Shades { get; } = AccentTone.Graphic(Identity);

    /// <summary>The accent as text, for the theme being rendered — the one part a theme changes.</summary>
    public AccentTextShades Text(bool dark) => dark ? _darkText : _lightText;

    private readonly AccentTextShades _darkText = AccentTone.Text(Identity, dark: true);

    private readonly AccentTextShades _lightText = AccentTone.Text(Identity, dark: false);

    /// <summary>The four accents from the design comp, each placed on the ladder's fill rung; blue
    /// (index 0) is the default and is what the rungs were measured from.</summary>
    public static readonly IReadOnlyList<AccentPreset> All = [
        new("Blue", Color.Parse("#4cc2ff")),
        new("Green", Color.Parse("#6dcc61")),
        new("Purple", Color.Parse("#d0a4ff")),
        new("Orange", Color.Parse("#ff9f79")),
    ];

    /// <summary>The default accent (blue), matching the comp.</summary>
    public static AccentPreset Default => All[0];
}
