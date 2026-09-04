using Avalonia.Media;

namespace DashDetective.Services.Theming;

/// <summary>The colour-vision deficiency the palettes are chosen for, from Settings → Accessibility.</summary>
public enum ColorVisionMode {
    /// <summary>The authored palettes.</summary>
    None,

    /// <summary>Green-weak/blind, the most common form.</summary>
    Deuteranopia,

    /// <summary>Red-weak/blind. Loses the same pairs as deuteranopia, so it shares its table.</summary>
    Protanopia,

    /// <summary>Blue-yellow. Loses a different axis, so it takes its own.</summary>
    Tritanopia,
}

/// <summary>The five status colours as a set, so a mode swaps them together.</summary>
public sealed record SemanticColors(Color Good, Color Warn, Color Bad, Color Info, Color Idle);

/// <summary>
/// The colour-blind-safe palettes, and the one place their hues are chosen.
///
/// Every table here is per-theme
///
/// The hues are Okabe-Ito, lightened or darkened for the theme. Red-green modes move "good" off green and
/// onto blue, because green beside red is the pair those deficiencies actually lose; tritanopia keeps
/// green and red, which it sees, and moves the middle step instead.
/// </summary>
public static class ColorVision {
    // ----- Status ---------------------------------------------------------

    private static readonly SemanticColors DarkRedGreen = new(
        Good: Color.Parse("#2CA1E3"), Warn: Color.Parse("#EDDE17"), Bad: Color.Parse("#B55000"),
        Info: Color.Parse("#A53F77"), Idle: Color.Parse("#BCC2C8"));

    private static readonly SemanticColors DarkTritan = new(
        Good: Color.Parse("#0EFFBD"), Warn: Color.Parse("#A16F00"), Bad: Color.Parse("#FF7101"),
        Info: Color.Parse("#0072B2"), Idle: Color.Parse("#BCC2C8"));

    private static readonly SemanticColors LightRedGreen = new(
        Good: Color.Parse("#0089D6"), Warn: Color.Parse("#9C920C"), Bad: Color.Parse("#753400"),
        Info: Color.Parse("#CC79A7"), Idle: Color.Parse("#3F4448"));

    private static readonly SemanticColors LightTritan = new(
        Good: Color.Parse("#009E73"), Warn: Color.Parse("#A16F00"), Bad: Color.Parse("#D55E00"),
        Info: Color.Parse("#003F62"), Idle: Color.Parse("#767C82"));

    // ----- Chart series ---------------------------------------------------
    //
    // NetDown repeats CPU and NetUp repeats Storage, exactly as the authored palette pairs them: the two
    // are drawn on one axis, so what matters is that THEY separate, and taking both from the verified
    // five means they inherit that set's guarantee rather than needing a rule of their own.

    private static readonly ChartSeriesColors DarkRedGreenSeries = new(
        Cpu: Color.Parse("#03A4FF"), Memory: Color.Parse("#A53F77"), Gpu: Color.Parse("#EDDE17"),
        Storage: Color.Parse("#A16F00"), NetDown: Color.Parse("#03A4FF"), NetUp: Color.Parse("#A16F00"),
        Threads: Color.Parse("#0EFFBD"));

    private static readonly ChartSeriesColors DarkTritanSeries = new(
        Cpu: Color.Parse("#FF7101"), Memory: Color.Parse("#CC79A7"), Gpu: Color.Parse("#00E5A7"),
        Storage: Color.Parse("#F4EC7B"), NetDown: Color.Parse("#FF7101"), NetUp: Color.Parse("#F4EC7B"),
        Threads: Color.Parse("#0072B2"));

    private static readonly ChartSeriesColors LightRedGreenSeries = new(
        Cpu: Color.Parse("#003F62"), Memory: Color.Parse("#CC79A7"), Gpu: Color.Parse("#9C920C"),
        Storage: Color.Parse("#753400"), NetDown: Color.Parse("#003F62"), NetUp: Color.Parse("#753400"),
        Threads: Color.Parse("#005C43"));

    private static readonly ChartSeriesColors LightTritanSeries = new(
        Cpu: Color.Parse("#D55E00"), Memory: Color.Parse("#81315E"), Gpu: Color.Parse("#009E73"),
        Storage: Color.Parse("#9C920C"), NetDown: Color.Parse("#D55E00"), NetUp: Color.Parse("#9C920C"),
        Threads: Color.Parse("#003F62"));

    /// <summary>The status set for a mode on a theme. <see cref="ColorVisionMode.None"/> keeps the
    /// authored colours, which is what makes the setting genuinely switchable off.</summary>
    public static SemanticColors Status(ColorVisionMode mode, bool dark) => mode switch {
        ColorVisionMode.Deuteranopia or ColorVisionMode.Protanopia => dark ? DarkRedGreen : LightRedGreen,
        ColorVisionMode.Tritanopia => dark ? DarkTritan : LightTritan,
        _ => Authored,
    };

    /// <summary>The chart series set for a mode, or <c>null</c> for <see cref="ColorVisionMode.None"/>,
    /// where the accent-derived palette applies instead.</summary>
    public static ChartSeriesColors? Series(ColorVisionMode mode, bool dark) => mode switch {
        ColorVisionMode.Deuteranopia or ColorVisionMode.Protanopia =>
            dark ? DarkRedGreenSeries : LightRedGreenSeries,
        ColorVisionMode.Tritanopia => dark ? DarkTritanSeries : LightTritanSeries,
        _ => null,
    };

    /// <summary>The app's own status colours, which every mode is measured against.</summary>
    public static SemanticColors Authored { get; } = new(
        Good: SemanticBrushes.GreenColor, Warn: SemanticBrushes.YellowColor,
        Bad: SemanticBrushes.RedColor, Info: SemanticBrushes.BlueColor,
        Idle: SemanticBrushes.NeutralColor);
}
