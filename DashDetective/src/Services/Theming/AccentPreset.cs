using Avalonia.Media;
using System.Collections.Generic;

namespace DashDetective.Services.Theming;

/// <summary>
/// One accent's shades for a single theme: the fill, its pointer-over step, the colour of text drawn on
/// it, and the brand gradient's bottom stop.
/// </summary>
public sealed record AccentShades(Color Fill, Color Hover, Color OnAccent, Color Deep);

/// <summary>
/// One selectable accent, in both themes. The two sets are authored rather than derived because they
/// answer opposite questions: on near-black the accent must be light, on white it must be dark enough to
/// read as text — every accent scored about 2:1 on white before the light set existed.
/// Immutable; the fixed set lives in <see cref="All"/>. Applied by <see cref="ThemeService"/>.
/// </summary>
public sealed record AccentPreset(string Name, AccentShades Dark, AccentShades Light) {
    /// <summary>The accent's identity hue, used to derive the chart palette and to fill the Settings
    /// swatch. Always the dark set's: it is what names the accent, not what a theme renders.</summary>
    public Color Color => Dark.Fill;

    /// <summary>The shades for the theme being rendered.</summary>
    public AccentShades For(bool dark) => dark ? Dark : Light;

    /// <summary>The four accents from the design comp; blue (index 0) is the default.</summary>
    public static readonly IReadOnlyList<AccentPreset> All = [
        Make("Blue",
             dark: ("#4cc2ff", "#66d0ff", "#06263a", "#2a7fd4"),
             light: ("#0078B6", "#00699F", "#FFFFFF", "#004C74")),
        Make("Green",
             dark: ("#6ccb5f", "#84d67a", "#0c2a10", "#3c9e4f"),
             light: ("#35822A", "#2F7325", "#FFFFFF", "#23551C")),
        Make("Purple",
             dark: ("#c58fff", "#d3a8ff", "#23103a", "#7d54c9"),
             light: ("#9A3BFF", "#8B20FF", "#FFFFFF", "#7100EB")),
        Make("Orange",
             dark: ("#ff8a5c", "#ff9f78", "#3a1606", "#d45f34"),
             light: ("#D43C00", "#BB3500", "#FFFFFF", "#8E2800")),
    ];

    /// <summary>The default accent (blue), matching the comp.</summary>
    public static AccentPreset Default => All[0];

    private static AccentPreset Make(
        string name,
        (string Fill, string Hover, string OnAccent, string Deep) dark,
        (string Fill, string Hover, string OnAccent, string Deep) light) =>
        new(name, Shades(dark), Shades(light));

    private static AccentShades Shades((string Fill, string Hover, string OnAccent, string Deep) hex) =>
        new(Color.Parse(hex.Fill), Color.Parse(hex.Hover),
            Color.Parse(hex.OnAccent), Color.Parse(hex.Deep));
}
