using Avalonia.Styling;

namespace DashDetective.Services.Theming;

/// <summary>
/// The app's own theme variants, beyond Avalonia's Light and Dark.
///
/// High contrast is a variant rather than a set of resource writes because a key declared inside
/// <c>ResourceDictionary.ThemeDictionaries</c> <b>cannot be shadowed</b> by writing the same key into
/// <c>Application.Resources</c> — the theme lookup wins, and the write is silently ignored. That is why
/// the accent and the chart series can be swapped at runtime (they are top-level keys) while the
/// surfaces and the text ramp cannot.
///
/// Each inherits from the plain variant it thickens, so a key the high-contrast dictionary does not
/// override falls back to Dark or Light and only the differences have to be authored.
/// </summary>
public static class AppVariants {
    public static ThemeVariant HighContrastDark { get; } = new("HighContrastDark", ThemeVariant.Dark);

    public static ThemeVariant HighContrastLight { get; } = new("HighContrastLight", ThemeVariant.Light);
}
