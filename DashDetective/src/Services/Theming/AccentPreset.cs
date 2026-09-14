using Avalonia.Media;
using System.Globalization;

namespace DashDetective.Services.Theming;

/// <summary>
/// One accent's graphic shades: the fill, its pointer-over step, the color of text drawn on it, and the
/// brand gradient's bottom stop.
/// </summary>
public sealed record AccentShades(Color Fill, Color Hover, Color OnAccent, Color Deep);

/// <summary>The accent drawn as text on the page: the color and its pointer-over step.</summary>
public sealed record AccentTextShades(Color Fill, Color Hover);

/// <summary>
/// One accent: a name and a single identity color, which is also its fill. Every shade comes from
/// <see cref="AccentTone"/>, so an accent keeps one appearance across themes.
///
/// Immutable. Blue is <see cref="Default"/>; any other color is <see cref="CustomName"/>. Applied by
/// <see cref="ThemeService"/>.
/// </summary>
public sealed record AccentPreset(string Name, Color Identity) {
    /// <summary>The name of an accent that is not the default.</summary>
    public const string CustomName = "Custom";

    /// <summary>The accent's identity, used to fill the Settings swatch.</summary>
    public Color Color => Identity;

    /// <summary>The identity as "#rrggbb", the form it persists in.</summary>
    public string Hex => $"#{Identity.R:x2}{Identity.G:x2}{Identity.B:x2}";

    /// <summary>The graphic shades. The same in both themes, which is what keeps the logo and the
    /// navigation highlight reading as one colour.</summary>
    public AccentShades Shades { get; } = AccentTone.Graphic(Identity);

    /// <summary>The accent as text, for the theme being rendered — the one part a theme changes.</summary>
    public AccentTextShades Text(bool dark) => dark ? _darkText : _lightText;

    private readonly AccentTextShades _darkText = AccentTone.Text(Identity, dark: true);

    private readonly AccentTextShades _lightText = AccentTone.Text(Identity, dark: false);

    /// <summary>The default accent (blue), matching the comp. It sits on the ladder's fill rung, which is
    /// what the rungs were measured from.</summary>
    public static AccentPreset Default { get; } = new("Blue", Color.Parse("#4cc2ff"));

    /// <summary><see cref="Default"/> for its own color, else a custom accent. Alpha is dropped: an accent
    /// is opaque.</summary>
    public static AccentPreset FromIdentity(Color identity) {
        var opaque = Color.FromRgb(identity.R, identity.G, identity.B);
        return opaque == Default.Identity ? Default : new AccentPreset(CustomName, opaque);
    }

    /// <summary>Resolves a persisted hex, or <see cref="Default"/> for an empty or unreadable one.</summary>
    public static AccentPreset FromHex(string? hex) =>
        TryParseHex(hex, out var color) ? FromIdentity(color) : Default;

    /// <summary>Reads "#rrggbb" or "#rgb", with or without the "#". Names and alpha are refused, so what is
    /// typed is what persists.</summary>
    public static bool TryParseHex(string? text, out Color color) {
        color = default;
        var digits = text?.Trim() ?? "";
        if (digits.StartsWith('#'))
            digits = digits[1..];
        if (digits.Length == 3)
            digits = $"{digits[0]}{digits[0]}{digits[1]}{digits[1]}{digits[2]}{digits[2]}";

        if (digits.Length != 6
            || !uint.TryParse(digits, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var rgb))
            return false;

        color = Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
        return true;
    }
}
