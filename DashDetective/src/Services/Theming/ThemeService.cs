using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using System;
using System.Collections.Generic;

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

    /// <summary>The color-vision mode in force. Anything but None overrides the accent's chart palette;
    /// the accent still drives the highlight.</summary>
    public ColorVisionMode ColorVision { get; private set; }

    /// <summary>Whether high contrast is in force. Composes with light/dark rather than replacing them,
    /// so "high contrast light" and "high contrast dark" both exist.</summary>
    public bool HighContrast { get; private set; }

    /// <summary>Whether the app is actually rendering dark right now. Differs from
    /// <see cref="CurrentTheme"/> under <see cref="AppTheme.System"/>, where the variant comes from the
    /// OS — so a "flip the theme" action can tell which way to flip. Kept here because this service
    /// owns the application's theme state; nothing else reads it either. Counts the high-contrast dark
    /// variant as dark, or the flip would go the wrong way while high contrast is on.</summary>
    public bool IsDarkVariantActive =>
        Application.Current?.ActualThemeVariant is { } variant
        && (variant == ThemeVariant.Dark || variant == AppVariants.HighContrastDark);

    /// <summary>Applies the current selections. Call once at startup after the app is built.</summary>
    public void ApplyDefaults() {
        ApplyTheme(CurrentTheme);
        ApplyDefaultAppearance();
    }

    /// <summary>Switches the light/dark/system colour scheme via the app's ThemeVariant.</summary>
    public void ApplyTheme(AppTheme theme) {
        CurrentTheme = theme;
        ApplyVariant();
    }

    /// <summary>Turns chart patterns on or off. A top-level key, so a plain resource write reaches it —
    /// unlike the surfaces, which need a variant.</summary>
    public void ApplyChartPatterns(bool enabled) {
        if (Application.Current is { } app)
            app.Resources["ChartPatterns"] = enabled;
    }

    /// <summary>Turns high contrast on or off — a separate axis from light/dark, so it composes with
    /// either. It has to be a ThemeVariant: a key inside <c>ThemeDictionaries</c> cannot be shadowed by
    /// writing to <c>Application.Resources</c>, the theme lookup wins and the write is ignored.</summary>
    public void ApplyContrast(bool enabled) {
        HighContrast = enabled;
        ApplyVariant();
    }

    /// <summary>Pushes the variant that the current theme and contrast selections imply.</summary>
    private void ApplyVariant() {
        if (Application.Current is not { } app)
            return;

        WatchOsTheme(app);
        app.RequestedThemeVariant = Variant();

        // The accent shades and the color-vision tables are both per-theme, so a theme change
        // reinstalls them.
        SetAccent(CurrentAccent ?? AccentPreset.Default);
        if (ColorVision != ColorVisionMode.None)
            ApplyVisionPalettes();
    }

    /// <summary>The variant for the current pair of selections.</summary>
    private ThemeVariant Variant() {
        if (!HighContrast)
            return CurrentTheme switch {
                AppTheme.Light => ThemeVariant.Light,
                AppTheme.Dark => ThemeVariant.Dark,
                _ => ThemeVariant.Default, // System: follow the OS setting.
            };

        // High contrast has no "follow the OS" variant of its own, so System is resolved to whichever of
        // light or dark the OS is actually showing.
        return IsDarkIntended() ? AppVariants.HighContrastDark : AppVariants.HighContrastLight;
    }

    private static bool OsPrefersDark(Application? app) =>
        app?.PlatformSettings?.GetColorValues().ThemeVariant == PlatformThemeVariant.Dark;

    /// <summary>Follows the OS scheme while high contrast is on under "System". Resolving System here is
    /// what takes Avalonia's automatic switch out of the picture, so it has to be replaced.</summary>
    private void WatchOsTheme(Application app) {
        if (_watchingOs || app.PlatformSettings is not { } settings)
            return;

        _watchingOs = true;
        settings.ColorValuesChanged += (_, _) => {
            if (HighContrast && CurrentTheme == AppTheme.System)
                Dispatcher.UIThread.Post(ApplyVariant);
        };
    }

    private bool _watchingOs;

    /// <summary>
    /// Restores the default look: blue highlight and distinct per-graph colours.
    /// </summary>
    public void ApplyDefaultAppearance() {
        CurrentAccent = null;
        SetAccent(AccentPreset.Default);
        SetChartSeries(SeriesForCurrentSelections());
    }

    /// <summary>Applies a color-vision mode: status brushes re-pointed, charts on its series palette
    /// instead of the accent-derived one.</summary>
    public void ApplyColorVision(ColorVisionMode mode) {
        ColorVision = mode;
        ApplyVisionPalettes();
    }

    /// <summary>Installs the tables for the current mode and theme. Re-run on a theme change: the safe
    /// colors differ by background, so one set cannot serve both.</summary>
    private void ApplyVisionPalettes() {
        SemanticBrushes.Apply(Theming.ColorVision.Status(ColorVision, IsDarkIntended()));
        SetChartSeries(SeriesForCurrentSelections());
    }

    /// <summary>The series palette the current selections imply. A color-vision mode beats the accent:
    /// rotating a safe palette by the accent's hue offset would undo what makes it safe.</summary>
    private ChartSeriesColors SeriesForCurrentSelections() =>
        Theming.ColorVision.Series(ColorVision, IsDarkIntended())
        ?? (CurrentAccent is { } accent ? ChartPalette.Derive(accent.Color) : ChartPalette.Default);

    /// <summary>Whether the app renders dark under the current selections. Read from the selection, not
    /// <c>ActualThemeVariant</c>, which has not caught up when the palettes are installed.</summary>
    public bool RendersDark => IsDarkIntended();

    private bool IsDarkIntended() => CurrentTheme switch {
        AppTheme.Light => false,
        AppTheme.Dark => true,
        _ => OsPrefersDark(Application.Current),
    };

    /// <summary>
    /// Applies a single accent: the highlight becomes <paramref name="accent"/> and the graphs take the
    /// palette derived from it, so each metric keeps a hue of its own.
    /// </summary>
    public void ApplyAccent(AccentPreset accent) {
        CurrentAccent = accent;
        SetAccent(accent);
        SetChartSeries(SeriesForCurrentSelections());
    }

    /// <summary>
    /// Installs the UI scale. <c>ScaleHost</c> transforms by <paramref name="scale"/>; the two popup
    /// surfaces that cannot host one — Fluent templates the tooltip and context-menu presenters — take
    /// <paramref name="popupFontSize"/> instead. Both are computed by <c>Services/Accessibility</c>;
    /// this only writes them, so the application still has a single writer.
    /// </summary>
    public void ApplyUiScale(double scale, double popupFontSize) {
        if (Application.Current is not { } app)
            return;

        app.Resources["UiScale"] = scale;
        app.Resources["PopupFontSize"] = popupFontSize;
    }

    /// <summary>Installs the type ladder. Takes the sizes already scaled, like
    /// <see cref="ApplyUiScale"/>, so the arithmetic stays in <c>Services/Accessibility</c> and this
    /// remains the application's single writer.</summary>
    public void ApplyTextScale(IReadOnlyDictionary<string, double> sizes) {
        if (Application.Current is not { } app)
            return;

        foreach (var (key, size) in sizes)
            app.Resources[key] = size;
    }

    /// <summary>Swaps the accent brushes, taking the shades for the theme being rendered. Every
    /// accent-colored element binds these keys with {DynamicResource}, so the change is global.</summary>
    private void SetAccent(AccentPreset accent) {
        if (Application.Current is not { } app)
            return;

        var shades = accent.For(IsDarkIntended());
        var res = app.Resources;
        res["Accent"] = new SolidColorBrush(shades.Fill);
        res["AccentHover"] = new SolidColorBrush(shades.Hover);
        res["OnAccent"] = new SolidColorBrush(shades.OnAccent);
        res["AccentSoft"] = new SolidColorBrush(shades.Fill, 0.12); // faint fill (e.g. sidebar highlight)
        res["AccentColor"] = shades.Fill;                           // brand-gradient top stop
        res["AccentDeep"] = shades.Deep;                            // brand-gradient bottom stop
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
