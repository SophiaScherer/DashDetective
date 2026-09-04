using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Xunit;

namespace DashDetective.Tests.Services.Theming;

/// <summary>
/// Measures Palette.axaml's text ramp against every surface it is drawn on, in all four variants, so
/// "high contrast" is a proven claim rather than an asserted one.
///
/// It reads the authored file rather than a copy of the numbers, because a copy is the thing that
/// drifts. The ramp is opacity over a surface, so <see cref="ContrastRatio"/> composites first — read
/// as plain white every entry would score 21:1 and this would pass while proving nothing.
/// </summary>
public class PaletteContrastTests {
    /// <summary>The surfaces body text is drawn on.</summary>
    private static readonly string[] Surfaces =
        ["AppBackground", "SidebarBackground", "PanelBackground", "CardBackground", "FieldBackground"];

    /// <summary>The ramp entries used for text a user has to read.</summary>
    private static readonly string[] BodyText =
        ["TextStrong", "TextPrimary", "TextSecondary", "TextTertiary", "TextMuted", "TextSubtle"];

    /// <summary>The two that are deliberately below body weight, and why. <c>TextFaint</c> is for
    /// decoration and dividers' labels; <c>TextGhost</c> is the completion suggestion sitting beside what
    /// is being typed, which has to read as a suggestion rather than as input. Neither is exempt in high
    /// contrast, where the whole point is that faded text stops being faded.</summary>
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

    /// <summary>
    /// The dark theme clears AA everywhere except <c>TextSubtle</c> on the three raised surfaces, where it
    /// lands at 4.36-4.48 against a 4.5 bar. Recorded rather than fixed: the ramp's steps are 5% apart, so
    /// lifting this one to clear AA puts it within 2% of <c>TextMuted</c> above it and the two stop being
    /// distinguishable — rebalancing the ramp is a design decision, not a test fix. High contrast is the
    /// answer offered today, and it takes every one of these past AAA.
    /// </summary>
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

    /// <summary>
    /// <b>The light theme does not meet AA across its whole ramp, and this records which pairs.</b> Dark
    /// text on a white card is a smaller step than white text on a near-black one at the same opacity, so
    /// the ramp that clears AA in dark falls short of it in light. Nothing is changed here: rebalancing
    /// the light ramp changes the app's normal appearance, which is a decision of its own — high contrast
    /// is the answer offered today. The list is asserted exactly, so a new failure fails the build and
    /// fixing one of these fails it too, with a message saying so.
    /// </summary>
    [Fact]
    public void Light_RecordsExactlyWhichBodyPairsMissAa() {
        var failing = Measure("Light", BodyText, ContrastRatio.AA)
            .Select(f => f.Split(':')[0])
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToArray();

        string[] known = [
            "TextMuted on AppBackground",
            "TextMuted on CardBackground",
            "TextMuted on FieldBackground",
            "TextMuted on PanelBackground",
            "TextMuted on SidebarBackground",
            "TextSubtle on AppBackground",
            "TextSubtle on CardBackground",
            "TextSubtle on FieldBackground",
            "TextSubtle on PanelBackground",
            "TextSubtle on SidebarBackground",
        ];

        Assert.Equal(known, failing);
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
        var brushes = Palette.Value;
        var text = Resolve(brushes, variant, textKey);
        var surface = Resolve(brushes, variant, surfaceKey);

        // A surface is opaque, so its own alpha never needs compositing.
        return ContrastRatio.Of(text.Color, text.Opacity, surface.Color);
    }

    /// <summary>A variant's value for a key, falling back to the variant it inherits from — which is how
    /// the high-contrast dictionaries get away with authoring only their differences.</summary>
    private static Brush Resolve(Dictionary<string, Dictionary<string, Brush>> brushes, string variant, string key) {
        if (brushes[variant].TryGetValue(key, out var brush))
            return brush;

        var parent = variant switch {
            "HighContrastDark" => "Dark",
            "HighContrastLight" => "Light",
            _ => throw new InvalidOperationException($"{variant} has no value for {key} and inherits nothing."),
        };

        return brushes[parent][key];
    }

    // ----- Reading the authored file -----

    private readonly record struct Brush((int R, int G, int B) Color, double Opacity);

    private static readonly Lazy<Dictionary<string, Dictionary<string, Brush>>> Palette = new(Read);

    /// <summary>One brush table per theme dictionary. Deliberately a plain parse of the authored XAML:
    /// loading it through Avalonia would need a render backend, which these tests do not have.</summary>
    private static Dictionary<string, Dictionary<string, Brush>> Read() {
        var xaml = File.ReadAllText(Path.Combine(SourceRoot(), "src/Shared/Styles/Palette.axaml"));
        var tables = new Dictionary<string, Dictionary<string, Brush>>(StringComparer.Ordinal);

        // Each theme dictionary opens with its key — "Dark"/"Light" plainly, the high-contrast pair
        // through an x:Static reference to AppVariants.
        var blocks = Regex.Matches(
            xaml,
            """<ResourceDictionary x:Key="(?:\{x:Static theming:AppVariants\.)?(\w+?)\}?">(.*?)</ResourceDictionary>""",
            RegexOptions.Singleline);

        foreach (Match block in blocks) {
            var table = new Dictionary<string, Brush>(StringComparer.Ordinal);

            foreach (Match entry in Regex.Matches(
                         block.Groups[2].Value,
                         """<SolidColorBrush x:Key="(\w+)" Color="#([0-9A-Fa-f]{6})"(?: Opacity="([\d.]+)")?\s*/>""")) {
                var hex = entry.Groups[2].Value;
                var opacity = entry.Groups[3].Success
                    ? double.Parse(entry.Groups[3].Value, CultureInfo.InvariantCulture)
                    : 1.0;

                table[entry.Groups[1].Value] = new Brush(
                    (Convert.ToInt32(hex[..2], 16), Convert.ToInt32(hex[2..4], 16), Convert.ToInt32(hex[4..], 16)),
                    opacity);
            }

            tables[block.Groups[1].Value] = table;
        }

        // A silent parse failure would make every assertion above vacuously true.
        foreach (var variant in new[] { "Dark", "Light", "HighContrastDark", "HighContrastLight" }) {
            Assert.True(tables.ContainsKey(variant), $"No theme dictionary parsed for {variant}.");
            Assert.NotEmpty(tables[variant]);
        }

        return tables;
    }

    /// <summary>Walks up to the repository from this file's own compile-time path, as
    /// <c>PaletteOwnershipTests</c> does: anchoring to the binaries breaks under
    /// <c>--artifacts-path</c>.</summary>
    private static string SourceRoot([CallerFilePath] string thisFile = "") {
        var dir = new DirectoryInfo(Path.GetDirectoryName(thisFile)!);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "DashDetective.sln")))
            dir = dir.Parent;

        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "DashDetective");
    }
}
