using Avalonia.Styling;

namespace DashDetective.Services.Theming;

/// <summary>
/// The app's own theme variants. High contrast is a variant, not a set of resource writes, because a key
/// inside <c>ThemeDictionaries</c> cannot be shadowed from <c>Application.Resources</c> — the theme
/// lookup wins silently. Each inherits its plain variant, so only the differences need authoring.
/// </summary>
public static class AppVariants {
    public static ThemeVariant HighContrastDark { get; } = new("HighContrastDark", ThemeVariant.Dark);

    public static ThemeVariant HighContrastLight { get; } = new("HighContrastLight", ThemeVariant.Light);
}
