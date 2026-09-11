using Avalonia.Media;
using Avalonia.Threading;

namespace DashDetective.Services.Theming;

/// <summary>
/// The fixed semantic colours, as brushes, for code that cannot reach {StaticResource} — status dots,
/// row VMs, icon catalogues. Mirrors the semantic keys in Palette.axaml, exactly as
/// <see cref="ChartPalette"/> mirrors the <c>Chart*</c> ones.
///
/// These deliberately do <b>not</b> follow the accent: a "Running" process should not turn orange
/// because the accent did. Anything that should re-hue is a <see cref="ChartSeries"/> and belongs to
/// <see cref="ThemeService.BrushFor"/> instead.
///
/// Value types over <c>Avalonia.Media</c> only — no render backend, so a test may touch this class
/// (unlike the <c>Geometry.Parse</c> icon catalogues that consume it).
/// </summary>
public static class SemanticBrushes {
    /// <summary>The tint alpha behind an icon tile or badge. One value for every hue, so tinted
    /// surfaces read as one family across tabs.</summary>
    private const double SoftAlpha = 0.16;

    // ----- Colour primitives, mirroring the <Color> keys in Palette.axaml -----

    public static Color BlueColor { get; } = Color.Parse("#4cc2ff");
    public static Color PurpleColor { get; } = Color.Parse("#c58fff");
    public static Color GreenColor { get; } = Color.Parse("#6ccb5f");
    public static Color YellowColor { get; } = Color.Parse("#ffcf4d");
    public static Color OrangeColor { get; } = Color.Parse("#ff8a5c");
    public static Color RedColor { get; } = Color.Parse("#ff6b6b");
    public static Color PinkColor { get; } = Color.Parse("#ff7ac6");
    public static Color NeutralColor { get; } = Color.Parse("#9aa0a6");

    // ----- Fixed hues -----

    public static IBrush Blue { get; } = new SolidColorBrush(BlueColor);
    public static IBrush Purple { get; } = new SolidColorBrush(PurpleColor);
    public static IBrush Green { get; } = new SolidColorBrush(GreenColor);
    public static IBrush Yellow { get; } = new SolidColorBrush(YellowColor);
    public static IBrush Orange { get; } = new SolidColorBrush(OrangeColor);
    public static IBrush Red { get; } = new SolidColorBrush(RedColor);
    public static IBrush Pink { get; } = new SolidColorBrush(PinkColor);
    public static IBrush Neutral { get; } = new SolidColorBrush(NeutralColor);

    // ----- Soft fills (the tinted tile or pill behind a glyph) -----

    public static IBrush BlueSoft { get; } = new SolidColorBrush(BlueColor, SoftAlpha);
    public static IBrush PurpleSoft { get; } = new SolidColorBrush(PurpleColor, SoftAlpha);
    public static IBrush GreenSoft { get; } = new SolidColorBrush(GreenColor, SoftAlpha);
    public static IBrush YellowSoft { get; } = new SolidColorBrush(YellowColor, SoftAlpha);
    public static IBrush OrangeSoft { get; } = new SolidColorBrush(OrangeColor, SoftAlpha);
    public static IBrush RedSoft { get; } = new SolidColorBrush(RedColor, SoftAlpha);

    // ----- Status: what a colour means, rather than which hue it is -----
    // Each is its OWN brush, not an alias of the fixed hue above: a color-vision mode mutates these, and
    // an alias would drag the file-type glyphs and icon tints along too.

    /// <summary>Healthy, running, connected, live.</summary>
    public static SolidColorBrush StatusGood { get; } = new(GreenColor);

    /// <summary>Degraded, suspended, transitional — not an error.</summary>
    public static SolidColorBrush StatusWarn { get; } = new(YellowColor);

    /// <summary>Failed or destructive.</summary>
    public static SolidColorBrush StatusBad { get; } = new(RedColor);

    /// <summary>Informational, or a live-but-not-physical thing (a virtual adapter).</summary>
    public static SolidColorBrush StatusInfo { get; } = new(BlueColor);

    /// <summary>Off, paused, disconnected, unknown.</summary>
    public static SolidColorBrush StatusIdle { get; } = new(NeutralColor);

    /// <summary>The soft fill paired with <see cref="StatusGood"/>.</summary>
    public static SolidColorBrush StatusGoodSoft { get; } = new(GreenColor, SoftAlpha);

    /// <summary>The soft fill paired with <see cref="StatusWarn"/>.</summary>
    public static SolidColorBrush StatusWarnSoft { get; } = new(YellowColor, SoftAlpha);

    // ----- Status, drawn as text rather than as a mark -----
    // The authored hues are for a near-black page: warn reads 1.47:1 on white, good 2.03:1, against a
    // 4.5:1 bar. A dot beside a label is a mark and keeps the hue; the label itself takes these. Same
    // split as Accent / AccentText and ChartCpu / ChartCpuText.

    /// <summary>The text counterpart of <see cref="StatusGood"/>.</summary>
    public static SolidColorBrush StatusGoodText { get; } = new(GreenColor);

    /// <summary>The text counterpart of <see cref="StatusWarn"/>.</summary>
    public static SolidColorBrush StatusWarnText { get; } = new(YellowColor);

    /// <summary>The text counterpart of <see cref="StatusBad"/>.</summary>
    public static SolidColorBrush StatusBadText { get; } = new(RedColor);

    /// <summary>The text counterpart of <see cref="StatusInfo"/>.</summary>
    public static SolidColorBrush StatusInfoText { get; } = new(BlueColor);

    /// <summary>The text counterpart of <see cref="StatusIdle"/>.</summary>
    public static SolidColorBrush StatusIdleText { get; } = new(NeutralColor);

    /// <summary>Re-points the status brushes for a color-vision mode and theme; mutating them repaints
    /// every consumer with no event. <paramref name="text"/> is the same set as the theme draws it as
    /// text, which differs from <paramref name="colors"/> only on the light theme. The hop is because
    /// <c>Color</c> is a styled property with UI-thread affinity — the app is always on it, xUnit is
    /// not.</summary>
    public static void Apply(SemanticColors colors, SemanticColors text) {
        if (!Dispatcher.UIThread.CheckAccess()) {
            Dispatcher.UIThread.Post(() => Apply(colors, text));
            return;
        }

        StatusGood.Color = colors.Good;
        StatusWarn.Color = colors.Warn;
        StatusBad.Color = colors.Bad;
        StatusInfo.Color = colors.Info;
        StatusIdle.Color = colors.Idle;
        StatusGoodSoft.Color = colors.Good;
        StatusWarnSoft.Color = colors.Warn;

        StatusGoodText.Color = text.Good;
        StatusWarnText.Color = text.Warn;
        StatusBadText.Color = text.Bad;
        StatusInfoText.Color = text.Info;
        StatusIdleText.Color = text.Idle;
    }
}
