using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using System;

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
///   <item><b>Single accent</b> — the highlight becomes the chosen colour and the graphs take a
///     palette <i>derived</i> from it (see <see cref="ChartPalette"/>). They are re-hued, never
///     flattened: painting all six the one accent colour left download and upload indistinguishable
///     on the same chart.</item>
/// </list>
///
/// This service applies but does not persist: it stays the single place that writes appearance to the
/// live application. Persistence is layered on separately — the composition root applies the saved
/// theme/accent through here at startup and observes changes to save them (see
/// <c>src/Services/Settings</c>), so the earlier "session-only by design" note no longer holds.
/// </summary>
public sealed class ThemeService {
    public AppTheme CurrentTheme { get; private set; } = AppTheme.Dark;

    /// <summary>The series colours currently in the resource dictionary. Read by a page that resolves
    /// its own brushes in code rather than through {DynamicResource} — the Performance tab.</summary>
    public ChartSeriesColors CurrentSeries { get; private set; } = ChartPalette.Default;

    /// <summary>Raised after the accent (and with it the chart palette) has been applied. A page that
    /// holds brushes rather than resource references re-resolves them here.</summary>
    public event Action<ChartSeriesColors>? SeriesChanged;

    /// <summary>One series' current colour as a brush. The seam for a page that assigns brushes in code
    /// rather than through {DynamicResource}, so it still reads from the one palette. Cached per palette,
    /// so re-applying an unchanged one hands back the same instance and changes nothing downstream.</summary>
    public IBrush BrushFor(ChartSeries series) => _seriesBrushes[(int)series];

    private IBrush[] _seriesBrushes = BuildSeriesBrushes(ChartPalette.Default);

    /// <summary>The chosen single accent, or <c>null</c> for the default multi-colour look.</summary>
    public AccentPreset? CurrentAccent { get; private set; }

    /// <summary>Whether the app is actually rendering dark right now. Differs from
    /// <see cref="CurrentTheme"/> under <see cref="AppTheme.System"/>, where the variant comes from the
    /// OS — so a "flip the theme" action can tell which way to flip. Kept here because this service
    /// owns the application's theme state; nothing else reads it either.</summary>
    public bool IsDarkVariantActive =>
        Application.Current?.ActualThemeVariant == ThemeVariant.Dark;

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
        SetChartSeries(ChartPalette.Default);
    }

    /// <summary>
    /// Applies a single accent: the highlight becomes <paramref name="accent"/> and the graphs take the
    /// palette derived from it, so each metric keeps a hue of its own.
    /// </summary>
    public void ApplyAccent(AccentPreset accent) {
        CurrentAccent = accent;
        SetAccent(accent);
        SetChartSeries(ChartPalette.Derive(accent.Color));
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

    /// <summary>Sets the per-graph chart brushes the dashboard binds to via {DynamicResource ...}, then
    /// announces them for the pages that hold brushes instead of resource references.</summary>
    private void SetChartSeries(ChartSeriesColors series) {
        CurrentSeries = series;
        _seriesBrushes = BuildSeriesBrushes(series);

        if (Application.Current is { } app) {
            var res = app.Resources;
            res["ChartCpu"] = new SolidColorBrush(series.Cpu);
            res["ChartMemory"] = new SolidColorBrush(series.Memory);
            res["ChartGpu"] = new SolidColorBrush(series.Gpu);
            res["ChartStorage"] = new SolidColorBrush(series.Storage);
            res["ChartNetDown"] = new SolidColorBrush(series.NetDown);
            res["ChartNetUp"] = new SolidColorBrush(series.NetUp);
            res["ChartThreads"] = new SolidColorBrush(series.Threads);
        }

        // Raised even with no Application (headless tests): the palette itself has still changed.
        SeriesChanged?.Invoke(series);
    }

    /// <summary>One brush per series, indexed by the enum so a reordered <see cref="ChartSeries"/> cannot
    /// silently mis-map.</summary>
    private static IBrush[] BuildSeriesBrushes(ChartSeriesColors series) {
        var values = Enum.GetValues<ChartSeries>();
        var brushes = new IBrush[values.Length];
        foreach (var value in values)
            brushes[(int)value] = new SolidColorBrush(series.For(value));
        return brushes;
    }
}
