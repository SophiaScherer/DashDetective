using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace DashDetective.Tests.Services.Theming;

/// <summary>
/// The console inset — Network's ping/DNS output and Toolkit's execution log — follows the theme. Its text
/// is measured against the inset itself, not white, since a light rung tuned for white misses AA on gray.
/// </summary>
public class PaletteConsoleTests {
    private static readonly string[] TextKeys = ["ConsoleFg", "ConsoleMuted", "ConsoleGreen", "ConsoleBlue"];

    private static readonly string[] AllKeys = ["ConsoleBg", .. TextKeys];

    [Theory]
    [InlineData("Dark", ContrastRatio.AA)]
    [InlineData("Light", ContrastRatio.AA)]
    [InlineData("HighContrastDark", ContrastRatio.AAA)]
    [InlineData("HighContrastLight", ContrastRatio.AAA)]
    public void EveryConsoleText_ClearsItsBarOnTheInset(string variant, double minimum) {
        var failures = TextKeys
            .Select(key => (key, ratio: Ratio(variant, key)))
            .Where(pair => pair.ratio < minimum)
            .Select(pair => $"{pair.key}: {pair.ratio:F2}:1")
            .ToList();

        Assert.True(failures.Count == 0, $"{variant} console text under {minimum}:1: " + string.Join(", ", failures));
    }

    [Theory]
    [InlineData("Dark", "HighContrastDark")]
    [InlineData("Light", "HighContrastLight")]
    public void HighContrast_IsNeverWorseThanThePlainVariant(string plain, string contrast) {
        foreach (var key in TextKeys)
            Assert.True(Ratio(contrast, key) >= Ratio(plain, key), $"{contrast} lowered {key}.");
    }

    /// <summary>A darker shade is still the same green and blue; only lightness moves.</summary>
    [Theory]
    [InlineData("Light", "ConsoleGreen", "#6ccb5f")]
    [InlineData("Light", "ConsoleBlue", "#4cc2ff")]
    [InlineData("HighContrastLight", "ConsoleGreen", "#6ccb5f")]
    [InlineData("HighContrastLight", "ConsoleBlue", "#4cc2ff")]
    public void ConsoleHues_KeepTheIdentityHueAndSaturation(string variant, string key, string identity) {
        var (r, g, b) = PaletteFile.Resolve(variant, key).Color;
        var shade = Color.FromRgb((byte)r, (byte)g, (byte)b).ToHsl();
        var authored = Color.Parse(identity).ToHsl();

        Assert.Equal(authored.H, shade.H, 1.0);
        Assert.Equal(authored.S, shade.S, 0.02);
    }

    /// <summary>Dark keeps the terminal look it always had.</summary>
    [Theory]
    [InlineData("ConsoleBg", "#111318")]
    [InlineData("ConsoleFg", "#c8ccd2")]
    [InlineData("ConsoleMuted", "#8b9099")]
    [InlineData("ConsoleGreen", "#6ccb5f")]
    [InlineData("ConsoleBlue", "#4cc2ff")]
    public void Dark_KeepsTheOriginalConsole(string key, string authored) {
        var expected = Color.Parse(authored);

        Assert.Equal(new PaletteFile.Brush((expected.R, expected.G, expected.B), 1.0), PaletteFile.Resolve("Dark", key));
    }

    /// <summary>The light inset matches the target field above it.</summary>
    [Fact]
    public void Light_InsetMatchesTheField() =>
        Assert.Equal(PaletteFile.Resolve("Light", "FieldBackground"), PaletteFile.Resolve("Light", "ConsoleBg"));

    /// <summary>A top-level copy would shadow the theme dictionaries and pin one look again.</summary>
    [Fact]
    public void ConsoleKeys_LiveOnlyInTheThemeDictionaries() {
        var stray = AllKeys.Where(PaletteFile.TopLevel.Contains).ToList();

        Assert.True(stray.Count == 0, "Authored at top level: " + string.Join(", ", stray));
    }

    private static double Ratio(string variant, string textKey) {
        var text = PaletteFile.Resolve(variant, textKey);
        var inset = PaletteFile.Resolve(variant, "ConsoleBg");

        return ContrastRatio.Of(text.Color, text.Opacity, inset.Color);
    }
}
