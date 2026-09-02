using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace DashDetective.Shared;

/// <summary>
/// Theme-dictionary lookups for visuals built in code rather than XAML. Code must ask with the
/// element's own <c>ActualThemeVariant</c>: a plain <c>FindResource</c> returns null for every brush
/// that lives in a theme dictionary, which is all of the palette.
/// </summary>
public static class ThemeResources {
    /// <summary>The themed brush for this key, or a solid fallback when the key is missing.</summary>
    public static IBrush Brush(this StyledElement element, string key, Color fallback) =>
        element.TryGetResource(key, element.ActualThemeVariant, out var found) && found is IBrush brush
            ? brush
            : new SolidColorBrush(fallback);

    /// <summary>The themed brush's colour, for a tint derived from it.</summary>
    public static Color BrushColor(this StyledElement element, string key, Color fallback) =>
        element.Brush(key, fallback) is ISolidColorBrush solid ? solid.Color : fallback;

    /// <summary>The themed corner radius for this key, as the single value the palette declares it
    /// with.</summary>
    public static double Radius(this StyledElement element, string key, double fallback) =>
        element.TryGetResource(key, element.ActualThemeVariant, out var found) && found is CornerRadius radius
            ? radius.TopLeft
            : fallback;

    /// <summary>The themed shadow for this key, or none. No literal fallback, unlike
    /// <see cref="Brush"/>: a shadow is decorative, so a missing key drops it rather than
    /// reintroducing a hex the palette is supposed to own.</summary>
    public static BoxShadows Shadow(this StyledElement element, string key) =>
        element.TryGetResource(key, element.ActualThemeVariant, out var found) && found is BoxShadows shadow
            ? shadow
            : default;
}
