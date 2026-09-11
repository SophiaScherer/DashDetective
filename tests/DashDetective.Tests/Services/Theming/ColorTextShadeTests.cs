using Avalonia.Media;
using DashDetective.Services.Theming;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace DashDetective.Tests.Services.Theming;

/// <summary>
/// Pins the split between a colour drawn as a graphic and the same colour drawn as text — the rule
/// <c>Accent</c> / <c>AccentText</c> established, extended to the chart series and the fixed hues. The
/// series are authored for a near-black page, so the figures drawn in them read about 2:1 on white; the
/// traces keep the authored colour, and only the text darkens.
/// </summary>
public class ColorTextShadeTests {
    /// <summary>The worst light surface for dark text is the lightest one, and three of the five are
    /// white.</summary>
    private static readonly (int R, int G, int B) White = (255, 255, 255);

    /// <summary>The lightest dark surface, which is the worst case for light text.</summary>
    private static readonly (int R, int G, int B) Card = (33, 33, 33);

    public static TheoryData<string> Series() =>
        ["Cpu", "Memory", "Gpu", "Storage", "NetDown", "NetUp", "Threads"];

    private static Color Of(ChartSeriesColors palette, string name) =>
        palette.For(System.Enum.Parse<ChartSeries>(name));

    /// <summary>The whole point: a figure drawn in a series colour has to be readable on a white page.</summary>
    [Theory]
    [MemberData(nameof(Series))]
    public void TextShade_MeetsAaOnWhite(string name) {
        var shade = Of(ChartPalette.TextShades(ChartPalette.Default), name);
        var ratio = ContrastRatio.Of((shade.R, shade.G, shade.B), 1.0, White);

        Assert.True(ratio >= ContrastRatio.AA,
            $"{name} reads at {ratio:F2}:1 as text on white.");
    }

    /// <summary>The authored series stay readable on the dark theme as they are, which is why only the
    /// light theme needs a shade at all.</summary>
    [Theory]
    [MemberData(nameof(Series))]
    public void AuthoredSeries_AlreadyMeetsAaOnTheDarkestSurfaceItIsDrawnOn(string name) {
        var color = Of(ChartPalette.Default, name);
        var ratio = ContrastRatio.Of((color.R, color.G, color.B), 1.0, Card);

        Assert.True(ratio >= ContrastRatio.AA,
            $"{name} reads at {ratio:F2}:1 as text on the card surface.");
    }

    /// <summary>A text shade is the same colour, only darker. A series that changed hue would stop being
    /// the series its trace names.</summary>
    [Theory]
    [MemberData(nameof(Series))]
    public void TextShade_KeepsTheSeriesHueAndSaturation(string name) {
        var authored = Of(ChartPalette.Default, name).ToHsl();
        var shade = Of(ChartPalette.TextShades(ChartPalette.Default), name).ToHsl();

        Assert.Equal(authored.H, shade.H, 1.0);
        Assert.Equal(authored.S, shade.S, 0.02);
    }

    /// <summary>The traces are untouched. Lifting them would undo the per-metric coding the charts depend
    /// on, and dark mode is meant to be unchanged by this work.</summary>
    [Fact]
    public void TextShades_LeaveTheAuthoredPaletteAlone() {
        var before = ChartPalette.Default;

        _ = ChartPalette.TextShades(before);

        Assert.Equal(before, ChartPalette.Default);
    }

    /// <summary>Every accent's derived palette has to survive the same treatment, not just the default
    /// look — the text shades are taken after the accent has re-hued the series.</summary>
    [Fact]
    public void TextShade_MeetsAaOnWhite_ForEveryAccent() {
        var failures = new List<string>();

        foreach (var accent in AccentPreset.All) {
            var text = ChartPalette.TextShades(ChartPalette.Derive(accent.Color));

            foreach (var series in System.Enum.GetValues<ChartSeries>()) {
                var shade = text.For(series);
                var ratio = ContrastRatio.Of((shade.R, shade.G, shade.B), 1.0, White);
                if (ratio < ContrastRatio.AA)
                    failures.Add($"{accent.Name}/{series}: {ratio:F2}:1");
            }
        }

        Assert.True(failures.Count == 0, string.Join(", ", failures));
    }

    /// <summary>A colour already at or below the rung is left where it is. This is what keeps the
    /// color-vision tables — searched against a light background, and tight on lightness — out of reach of
    /// a blanket darkening that would collapse their separation.</summary>
    [Fact]
    public void TextOnLight_NeverLightensAColourThatIsAlreadyDarkEnough() {
        var alreadyDark = ColorVision.Series(ColorVisionMode.Deuteranopia, dark: false)!;

        foreach (var series in System.Enum.GetValues<ChartSeries>()) {
            var color = alreadyDark.For(series);
            if (Tone.Lightness(color) <= Tone.LightText)
                Assert.Equal(color, Tone.TextOnLight(color));
        }
    }

    /// <summary>The fixed hues authored into the light theme dictionary have to be what the rule
    /// produces, or the palette and the ladder would disagree about the same colour.</summary>
    [Theory]
    [InlineData("BlueText", "#4cc2ff")]
    [InlineData("OrangeText", "#ff8a5c")]
    [InlineData("GreenText", "#6ccb5f")]
    public void LightDictionary_TextHues_AreTheRulesOutput(string key, string identity) {
        var authored = PaletteFile.Resolve("Light", key);
        var expected = Tone.TextOnLight(Color.Parse(identity));

        Assert.Equal((expected.R, expected.G, expected.B), authored.Color);
    }

    /// <summary>The dark theme draws the hues at their authored lightness, so the pair only differs where
    /// it has to.</summary>
    [Theory]
    [InlineData("BlueText", "#4cc2ff")]
    [InlineData("OrangeText", "#ff8a5c")]
    [InlineData("GreenText", "#6ccb5f")]
    public void DarkDictionary_TextHues_AreTheIdentityItself(string key, string identity) {
        var authored = PaletteFile.Resolve("Dark", key);
        var expected = Color.Parse(identity);

        Assert.Equal((expected.R, expected.G, expected.B), authored.Color);
    }

    /// <summary>The status hues are the worst offenders — warn reads 1.47:1 on white, the lowest in the
    /// app — and each is drawn as a word as well as a dot: the Processes Status column, the adapter's
    /// state, the connections table, the drive health pill.</summary>
    [Fact]
    public void StatusTextShades_MeetAaOnWhite() {
        var authored = ColorVision.Authored;
        var failures = new List<string>();

        foreach (var (name, color) in new[] {
            ("Good", authored.Good), ("Warn", authored.Warn), ("Bad", authored.Bad),
            ("Info", authored.Info), ("Idle", authored.Idle),
        }) {
            var shade = Tone.TextOnLight(color);
            var ratio = ContrastRatio.Of((shade.R, shade.G, shade.B), 1.0, White);
            if (ratio < ContrastRatio.AA)
                failures.Add($"{name}: {ratio:F2}:1");
        }

        Assert.True(failures.Count == 0, string.Join(", ", failures));
    }

    /// <summary>A status keeps its meaning across the split: the word and the dot beside it must not read
    /// as two different colours.</summary>
    [Fact]
    public void StatusTextShades_KeepTheHueOfTheMarkTheySitBeside() {
        var authored = ColorVision.Authored;

        foreach (var color in new[] {
            authored.Good, authored.Warn, authored.Bad, authored.Info, authored.Idle,
        }) {
            var shade = Tone.TextOnLight(color);

            Assert.Equal(color.ToHsl().H, shade.ToHsl().H, 1.0);
            Assert.Equal(color.ToHsl().S, shade.ToHsl().S, 0.02);
        }
    }

    /// <summary>Every key ThemeService writes has to exist in the authored palette, or the app renders a
    /// frame of nothing before the first apply — and a typo in either list would go unnoticed.</summary>
    [Fact]
    public void EveryTextKey_IsAuthoredInThePalette() {
        string[] keys = [
            "ChartCpuText", "ChartMemoryText", "ChartGpuText", "ChartStorageText",
            "ChartNetDownText", "ChartNetUpText", "ChartThreadsText", "StatusWarnText",
        ];

        var missing = keys.Where(key => !PaletteFile.TopLevel.Contains(key)).ToList();

        Assert.True(missing.Count == 0, "Not authored in Palette.axaml: " + string.Join(", ", missing));
    }
}
