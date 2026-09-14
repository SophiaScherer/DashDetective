using Avalonia.Controls;
using Avalonia.Media;

namespace DashDetective.Services.Theming;

/// <summary>
/// The accent's resource keys and what goes in each. <see cref="ThemeService"/> writes them to the
/// application; the Settings preview writes them to its own subtree, so it renders a draft through the
/// real styles.
/// </summary>
internal static class AccentResources {
    /// <summary>The faint fill behind a selected row or a reveal flash.</summary>
    private const double SoftAlpha = 0.12;

    /// <summary>Writes every accent key for <paramref name="accent"/>. Only the text pair depends on
    /// <paramref name="dark"/>.</summary>
    internal static void Write(IResourceDictionary resources, AccentPreset accent, bool dark) {
        var shades = accent.Shades;
        var text = accent.Text(dark);
        resources["Accent"] = new SolidColorBrush(shades.Fill);
        resources["AccentHover"] = new SolidColorBrush(shades.Hover);
        resources["OnAccent"] = new SolidColorBrush(shades.OnAccent);
        resources["AccentSoft"] = new SolidColorBrush(shades.Fill, SoftAlpha);
        resources["AccentColor"] = shades.Fill;                   // brand-gradient top stop
        resources["AccentDeep"] = shades.Deep;                    // brand-gradient bottom stop
        resources["AccentText"] = new SolidColorBrush(text.Fill); // the accent drawn as page text
        resources["AccentTextHover"] = new SolidColorBrush(text.Hover);
    }
}
