using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;

namespace DashDetective.Services.Theming;

/// <summary>
/// The single place that applies appearance settings to the live application. Views and
/// view-models ask this service to change the theme or accent; nothing else writes to the
/// application resource dictionary or <see cref="Application.RequestedThemeVariant"/>.
///
/// Accent selection has two modes:
/// <list type="bullet">
///   <item><b>Default</b> (multi-colour) — the highlight is blue and each dashboard graph keeps a
///     distinct colour. This is the startup look. <see cref="CurrentAccent"/> is <c>null</c>.</item>
///   <item><b>Single accent</b> — the highlight becomes the chosen colour and every graph is
///     recoloured to match it.</item>
/// </list>
///
/// Session-only by design: selections are not persisted, so each run starts from the defaults.
/// </summary>
public sealed class ThemeService {
    // Default per-graph colours for the multi-colour look (mirror the Chart* defaults in Palette.axaml).
    private static readonly Color CpuDefault = Color.Parse("#4cc2ff");
    private static readonly Color MemoryDefault = Color.Parse("#c58fff");
    private static readonly Color GpuDefault = Color.Parse("#6ccb5f");
    private static readonly Color StorageDefault = Color.Parse("#ffcf4d");
    private static readonly Color NetUpDefault = Color.Parse("#ff8a5c");

    public AppTheme CurrentTheme { get; private set; } = AppTheme.Dark;

    /// <summary>The chosen single accent, or <c>null</c> for the default multi-colour look.</summary>
    public AccentPreset? CurrentAccent { get; private set; }

    /// <summary>Applies the current selections. Call once at startup after the app is built.</summary>
    public void ApplyDefaults() {
        ApplyTheme(CurrentTheme);
        ApplyDefaultAppearance();
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
    /// Restores the default look: blue highlight and distinct per-graph colours.
    /// </summary>
    public void ApplyDefaultAppearance() {
        CurrentAccent = null;
        SetAccent(AccentPreset.Default);
        SetChartSeries(CpuDefault, MemoryDefault, GpuDefault, StorageDefault, CpuDefault, NetUpDefault);
    }

    /// <summary>
    /// Applies a single accent: the highlight becomes <paramref name="accent"/> and every graph is
    /// recoloured to match it.
    /// </summary>
    public void ApplyAccent(AccentPreset accent) {
        CurrentAccent = accent;
        SetAccent(accent);
        var c = accent.Color;
        SetChartSeries(c, c, c, c, c, c);
    }

    /// <summary>
    /// Swaps the accent brushes in the application resource dictionary. Every accent-coloured
    /// element references these keys via {DynamicResource ...}, so the change is instant and global.
    /// </summary>
    private static void SetAccent(AccentPreset accent) {
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

    /// <summary>Sets the per-graph chart brushes the dashboard binds to via {DynamicResource ...}.</summary>
    private static void SetChartSeries(Color cpu, Color memory, Color gpu, Color storage, Color netDown, Color netUp) {
        if (Application.Current is not { } app)
            return;

        var res = app.Resources;
        res["ChartCpu"] = new SolidColorBrush(cpu);
        res["ChartMemory"] = new SolidColorBrush(memory);
        res["ChartGpu"] = new SolidColorBrush(gpu);
        res["ChartStorage"] = new SolidColorBrush(storage);
        res["ChartNetDown"] = new SolidColorBrush(netDown);
        res["ChartNetUp"] = new SolidColorBrush(netUp);
    }
}
