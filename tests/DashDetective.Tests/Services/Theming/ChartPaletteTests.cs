using Avalonia.Media;
using DashDetective.Services.Theming;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace DashDetective.Tests.Services.Theming;

/// <summary>Covers <see cref="ChartPalette"/>: an accent must re-hue the graphs, never flatten them.
/// Painting all six series the accent's colour is what made download and upload one indistinguishable
/// line on the throughput chart, so the distinctness assertions below are the regression this pins.</summary>
public class ChartPaletteTests {
    /// <summary>The blue accent and the "Default" swatch have to agree, or picking blue would visibly
    /// differ from the look the app starts in.</summary>
    [Fact]
    public void Derive_DefaultAccent_ReproducesTheAuthoredPalette() {
        var derived = ChartPalette.Derive(AccentPreset.Default.Color);

        Assert.Equal(ChartPalette.Default, derived);
    }

    [Fact]
    public void Derive_CpuAndNetDown_AreTheAccentItself() {
        foreach (var accent in AccentPreset.All) {
            var palette = ChartPalette.Derive(accent.Color);

            Assert.Equal(accent.Color, palette.Cpu);
            Assert.Equal(accent.Color, palette.NetDown);
        }
    }

    /// <summary>The point of the whole exercise: whichever accent is picked, the metrics stay
    /// distinguishable from one another.</summary>
    [Fact]
    public void Derive_EveryAccent_KeepsTheMetricsOnDistinctHues() {
        foreach (var accent in AccentPreset.All) {
            var hues = Hues(ChartPalette.Derive(accent.Color));

            Assert.Equal(hues.Count, hues.Distinct().Count());
        }
    }

    /// <summary>The authored spacing between the hues is what makes the palette readable, so a rotation
    /// has to carry it across intact rather than merely producing five different colours.</summary>
    [Fact]
    public void Derive_PreservesTheGapsBetweenTheAuthoredHues() {
        var reference = Gaps(ChartPalette.Default);

        foreach (var accent in AccentPreset.All)
            Assert.Equal(reference, Gaps(ChartPalette.Derive(accent.Color)), Comparer());
    }

    /// <summary>Saturation and lightness are what keep a series legible on both themes, so only the hue
    /// may turn.</summary>
    [Fact]
    public void Derive_LeavesSaturationAndLightnessAlone() {
        foreach (var accent in AccentPreset.All) {
            var palette = ChartPalette.Derive(accent.Color);

            AssertSameSaturationAndLightness(ChartPalette.Default.Memory, palette.Memory);
            AssertSameSaturationAndLightness(ChartPalette.Default.Gpu, palette.Gpu);
            AssertSameSaturationAndLightness(ChartPalette.Default.Storage, palette.Storage);
            AssertSameSaturationAndLightness(ChartPalette.Default.NetUp, palette.NetUp);
        }
    }

    /// <summary>A turn that carries a hue past 360° has to wrap rather than land outside the wheel — the
    /// orange accent turns the palette far enough to do exactly that.</summary>
    [Fact]
    public void Derive_WrapsHuesPastTheEndOfTheWheel() {
        var palette = ChartPalette.Derive(Color.Parse("#ff8a5c"));

        foreach (var hue in Hues(palette))
            Assert.InRange(hue, 0, 360);
    }

    private static void AssertSameSaturationAndLightness(Color expected, Color actual) {
        Assert.Equal(expected.ToHsl().S, actual.ToHsl().S, 2);
        Assert.Equal(expected.ToHsl().L, actual.ToHsl().L, 2);
    }

    private static List<double> Hues(ChartSeriesColors palette) => new() {
        palette.Cpu.ToHsl().H, palette.Memory.ToHsl().H, palette.Gpu.ToHsl().H,
        palette.Storage.ToHsl().H, palette.NetUp.ToHsl().H,
    };

    /// <summary>Each series' hue distance from CPU's, walking the wheel one way, so the shape of the
    /// palette can be compared independently of where it now sits.</summary>
    private static List<double> Gaps(ChartSeriesColors palette) {
        var cpu = palette.Cpu.ToHsl().H;
        return Hues(palette).Select(h => (h - cpu + 360) % 360).ToList();
    }

    /// <summary>Hue maths round-trips through RGB bytes, so compare to the nearest degree.</summary>
    private static IEqualityComparer<double> Comparer() => new DegreeComparer();

    private sealed class DegreeComparer : IEqualityComparer<double> {
        public bool Equals(double a, double b) => Math.Abs(a - b) < 1.0;

        public int GetHashCode(double value) => 0;
    }
}
