using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;

namespace DashDetective.Services.Theming;

/// <summary>
/// The single place that applies appearance settings to the live application. Views and
/// view-models ask this service to change the theme or accent; nothing else writes to the
/// application resource dictionary or <see cref="Application.RequestedThemeVariant"/>.
///
/// Session-only by design: selections are not persisted, so each run starts from the defaults
/// (Dark + Blue). A persistence layer can later seed <see cref="CurrentTheme"/>/<see cref="CurrentAccent"/>
/// before <see cref="ApplyDefaults"/> without changing anything here.
/// </summary>
public sealed class ThemeService {
    public AppTheme CurrentTheme { get; private set; } = AppTheme.Dark;
    public AccentPreset CurrentAccent { get; private set; } = AccentPreset.Default;

    /// <summary>Applies the current selections. Call once at startup after the app is built.</summary>
    public void ApplyDefaults() {
        ApplyTheme(CurrentTheme);
        ApplyAccent(CurrentAccent);
    }

    /// <summary>Switches the light/dark/system colour scheme via the app's ThemeVariant.</summary>
    public void ApplyTheme(AppTheme theme) {
        CurrentTheme = theme;
        if (Application.Current is { } app)
            app.RequestedThemeVariant = theme switch {
                AppTheme.Light => ThemeVariant.Light,
                AppTheme.Dark => ThemeVariant.Dark,
                _ => ThemeVariant.Default, // System: follow the OS setting.
            };
    }

    /// <summary>
    /// Swaps the accent brushes in the application resource dictionary. Every accent-coloured
    /// element references these keys via {DynamicResource ...}, so the change is instant and global.
    /// </summary>
    public void ApplyAccent(AccentPreset accent) {
        CurrentAccent = accent;
        if (Application.Current is not { } app)
            return;

        var res = app.Resources;
        res["Accent"] = new SolidColorBrush(accent.Color);
        res["AccentHover"] = new SolidColorBrush(accent.Hover);
        res["OnAccent"] = new SolidColorBrush(accent.OnAccent);
        res["AccentSoft"] = new SolidColorBrush(accent.Color, 0.12); // faint fill (e.g. sidebar highlight)
        res["AccentColor"] = accent.Color;                            // brand-gradient top stop
        res["AccentDeep"] = accent.Deep;                              // brand-gradient bottom stop
    }
}
