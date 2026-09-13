using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace DashDetective.Tests.Services.Theming;

/// <summary>
/// Measures Palette.axaml's text ramp against every surface it is drawn on, in all four variants. It
/// reads the authored file rather than a copy, and composites opacity onto the surface first — read as
/// plain white, every ramp entry would score 21:1 and this would pass while proving nothing.
/// </summary>
public class PaletteContrastTests {
    /// <summary>The surfaces body text is drawn on.</summary>
    private static readonly string[] Surfaces =
        ["AppBackground", "SidebarBackground", "PanelBackground", "CardBackground", "FieldBackground"];

    /// <summary>The ramp entries used for text a user has to read.</summary>
    private static readonly string[] BodyText =
        ["TextStrong", "TextPrimary", "TextSecondary", "TextTertiary", "TextMuted", "TextSubtle"];

    /// <summary>The two deliberately below body weight: decoration, and the completion suggestion that
    /// must read as a suggestion. Neither is exempt in high contrast.</summary>
    private static readonly string[] BelowBodyWeight = ["TextFaint", "TextGhost"];

    [Theory]
    [InlineData("HighContrastDark")]
    [InlineData("HighContrastLight")]
    public void HighContrast_EveryBodyTextOnEverySurface_MeetsAaa(string variant) {
        var failures = Measure(variant, BodyText, ContrastRatio.AAA);

        Assert.True(failures.Count == 0,
            $"High contrast promises AAA ({ContrastRatio.AAA}:1) and these pairs fall short:" +
            Environment.NewLine + string.Join(Environment.NewLine, failures));
    }

    /// <summary>The faint pair is exempt in the normal themes but not here: high contrast exists to
    /// remove exactly this kind of translucency, so it has to reach AA at minimum.</summary>
    [Theory]
    [InlineData("HighContrastDark")]
    [InlineData("HighContrastLight")]
    public void HighContrast_LiftsTheFaintEntriesToAtLeastAa(string variant) {
        var failures = Measure(variant, BelowBodyWeight, ContrastRatio.AA);

        Assert.True(failures.Count == 0,
            "High contrast must lift the faint entries too:" +
            Environment.NewLine + string.Join(Environment.NewLine, failures));
    }

    /// <summary>High contrast has to be a real improvement on every pair, not just on average — a
    /// variant that raised one entry and lowered another would still pass the AAA test above.</summary>
    [Theory]
    [InlineData("Dark", "HighContrastDark")]
    [InlineData("Light", "HighContrastLight")]
    public void HighContrast_IsNeverWorseThanThePlainVariant(string plain, string contrast) {
        var regressions = new List<string>();

        foreach (var text in BodyText.Concat(BelowBodyWeight))
            foreach (var surface in Surfaces) {
                var before = Ratio(plain, text, surface);
                var after = Ratio(contrast, text, surface);
                if (after < before)
                    regressions.Add($"{contrast}: {text} on {surface} fell {before:F2} -> {after:F2}");
            }

        Assert.True(regressions.Count == 0,
            "High contrast lowered these:" + Environment.NewLine + string.Join(Environment.NewLine, regressions));
    }

    // ----- The shipped themes -----

    /// <summary>Dark clears AA except <c>TextSubtle</c> on the three raised surfaces, at 4.36-4.48
    /// against 4.5. Recorded, not fixed: the ramp's steps are 5% apart, so lifting this one collapses it
    /// into <c>TextMuted</c>. Rebalancing is a design decision; high contrast is the answer today.</summary>
    [Fact]
    public void Dark_RecordsExactlyWhichBodyPairsMissAa() {
        var failing = Measure("Dark", BodyText, ContrastRatio.AA)
            .Select(f => f.Split(':')[0])
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToArray();

        string[] known = [
            "TextSubtle on CardBackground",
            "TextSubtle on PanelBackground",
            "TextSubtle on SidebarBackground",
        ];

        Assert.Equal(known, failing);
    }

    /// <summary>Light clears AA outright. It did not before the ramp was respaced on CIE L*:
    /// <c>TextMuted</c> and <c>TextSubtle</c> missed on every surface, and the equal-opacity ramp left no
    /// room above them to lift into. Dark keeps its own ramp and its own recorded miss.</summary>
    [Fact]
    public void Light_EveryBodyTextOnEverySurface_MeetsAa() {
        var failures = Measure("Light", BodyText, ContrastRatio.AA);

        Assert.True(failures.Count == 0,
            "Light promises AA for body text and these pairs fall short:" +
            Environment.NewLine + string.Join(Environment.NewLine, failures));
    }

    // ----- Measuring -----

    private static List<string> Measure(string variant, string[] textKeys, double minimum) {
        var failures = new List<string>();

        foreach (var text in textKeys)
            foreach (var surface in Surfaces) {
                var ratio = Ratio(variant, text, surface);
                if (ratio < minimum)
                    failures.Add($"{text} on {surface}: {ratio:F2}:1");
            }

        return failures;
    }

    private static double Ratio(string variant, string textKey, string surfaceKey) {
        var text = PaletteFile.Resolve(variant, textKey);
        var surface = PaletteFile.Resolve(variant, surfaceKey);

        // A surface is opaque, so its own alpha never needs compositing.
        return ContrastRatio.Of(text.Color, text.Opacity, surface.Color);
    }
}
