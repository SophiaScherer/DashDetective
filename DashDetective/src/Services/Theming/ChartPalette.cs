using Avalonia.Media;
using System;

namespace DashDetective.Services.Theming;

/// <summary>Which graph a colour belongs to. What a view holding brushes in code names instead of a
/// resource key — the Performance tab's rows, which cannot reach {DynamicResource}.</summary>
public enum ChartSeries {
    Cpu,
    Memory,
    Gpu,
    Storage,
    NetDown,
    NetUp,
    Threads,
}

/// <summary>
/// The seven per-graph series colours, in the order <c>ThemeService</c> writes them to the
/// <c>ChartCpu</c> / <c>ChartMemory</c> / <c>ChartGpu</c> / <c>ChartStorage</c> / <c>ChartNetDown</c> /
/// <c>ChartNetUp</c> / <c>ChartThreads</c> resource keys.
/// </summary>
public sealed record ChartSeriesColors(
    Color Cpu, Color Memory, Color Gpu, Color Storage, Color NetDown, Color NetUp, Color Threads) {

    /// <summary>This palette's colour for one series. Every member is listed explicitly: the fallback
    /// arm would silently paint a newly added series as CPU rather than fail to compile.</summary>
    public Color For(ChartSeries series) => series switch {
        ChartSeries.Memory => Memory,
        ChartSeries.Gpu => Gpu,
        ChartSeries.Storage => Storage,
        ChartSeries.NetDown => NetDown,
        ChartSeries.NetUp => NetUp,
        ChartSeries.Threads => Threads,
        _ => Cpu,
    };
}

/// <summary>
/// The one source of truth for chart series colours, for both the default look and every accent.
///
/// Selecting an accent used to paint all six series the accent's colour, which erased the per-metric
/// coding the charts depend on — most visibly on the two-series throughput chart, where download and
/// upload became one indistinguishable line. Instead an accent now yields a <b>rotated</b> palette:
/// the accent is the CPU (and net-down) series, and every other series keeps its own saturation and
/// lightness while its hue turns by the same angle the accent turned from the default blue. The
/// authored spacing between the hues is therefore preserved whatever the accent, and
/// <c>Derive(AccentPreset.Default.Color)</c> reproduces <see cref="Default"/> exactly, so the blue
/// accent and the "Default" swatch agree.
///
/// Pure colour maths over <c>Avalonia.Media</c> value types — no render backend, so it is unit-testable.
/// </summary>
public static class ChartPalette {
    /// <summary>The authored per-graph colours. Mirrors the <c>Chart*</c> defaults in Palette.axaml.</summary>
    public static ChartSeriesColors Default { get; } = new(
        Cpu: Color.Parse("#4cc2ff"),
        Memory: Color.Parse("#c58fff"),
        Gpu: Color.Parse("#6ccb5f"),
        Storage: Color.Parse("#ffcf4d"),
        NetDown: Color.Parse("#4cc2ff"),
        NetUp: Color.Parse("#ff8a5c"),
        // Pink sits in the wheel's one wide gap, between purple and orange, so a thread count reads as
        // its own thing rather than as a second GPU or memory figure.
        Threads: Color.Parse("#ff7ac6"));

    /// <summary>The palette for <paramref name="accent"/>: the accent itself for CPU and net-down, and
    /// every other series' default hue turned by the accent's offset from the default blue.</summary>
    public static ChartSeriesColors Derive(Color accent) {
        var turn = accent.ToHsl().H - Default.Cpu.ToHsl().H;
        return new ChartSeriesColors(
            Cpu: accent,
            Memory: Rotate(Default.Memory, turn),
            Gpu: Rotate(Default.Gpu, turn),
            Storage: Rotate(Default.Storage, turn),
            NetDown: accent,
            NetUp: Rotate(Default.NetUp, turn),
            Threads: Rotate(Default.Threads, turn));
    }

    /// <summary>Turns <paramref name="color"/>'s hue by <paramref name="degrees"/>, keeping its
    /// saturation, lightness and alpha — so a rotated palette stays as readable as the authored one.</summary>
    private static Color Rotate(Color color, double degrees) {
        var hsl = color.ToHsl();
        return HslColor.FromAhsl(hsl.A, Wrap(hsl.H + degrees), hsl.S, hsl.L).ToRgb();
    }

    /// <summary>Normalises an angle into [0, 360), which <c>HslColor</c> requires.</summary>
    private static double Wrap(double degrees) {
        var wrapped = degrees % 360;
        return wrapped < 0 ? wrapped + 360 : wrapped;
    }
}
