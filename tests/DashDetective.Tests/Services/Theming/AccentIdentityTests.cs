using Avalonia.Media;
using DashDetective.Services.Theming;
using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Xunit;

namespace DashDetective.Tests.Services.Theming;

/// <summary>
/// Pins the accent model: one identity per accent, one shared lightness ladder, and graphic shades that
/// do not follow the theme. This is what stops the shades drifting back apart, which is how the
/// hand-authored dark set diverged from the light one in the first place.
/// </summary>
public class AccentIdentityTests {
    public static TheoryData<string> Accents() => [.. AccentPreset.All.Select(a => a.Name)];

    private static AccentPreset Preset(string name) => AccentPreset.All.First(a => a.Name == name);

    /// <summary>An identity is also its fill, so one edited off the rung would silently give that accent
    /// a different weight from the other three.</summary>
    [Theory]
    [MemberData(nameof(Accents))]
    public void EveryIdentity_SitsOnTheLaddersFillRung(string name) {
        Assert.Equal(AccentTone.Fill, Tone.Lightness(Preset(name).Identity), 0.5);
    }

    /// <summary>What the bug asked for: the logo, the navigation highlight and the picker swatch are the
    /// same colour whichever theme is on. Only the text shade may differ.</summary>
    [Theory]
    [MemberData(nameof(Accents))]
    public void GraphicShades_DoNotFollowTheTheme(string name) {
        var preset = Preset(name);

        Assert.Equal(preset.Identity, preset.Shades.Fill);
        Assert.Equal(AccentTone.Graphic(preset.Identity), preset.Shades);
    }

    /// <summary>The bug this model replaces: a hue that turned up to 19 degrees and lost 48 points of
    /// saturation between themes.</summary>
    [Theory]
    [MemberData(nameof(Accents))]
    public void EveryShade_KeepsItsIdentity(string name) {
        var preset = Preset(name);
        var identity = preset.Identity.ToHsl();

        Color[] shades = [
            preset.Shades.Fill, preset.Shades.Hover, preset.Shades.Deep,
            preset.Text(dark: true).Fill, preset.Text(dark: true).Hover,
            preset.Text(dark: false).Fill, preset.Text(dark: false).Hover,
        ];

        foreach (var shade in shades) {
            Assert.Equal(identity.H, shade.ToHsl().H, 1.0);
            Assert.Equal(identity.S, shade.ToHsl().S, 0.02);
        }
    }

    /// <summary>The complaint behind the bug: each accent used to move a different distance when the
    /// theme flipped. Now every accent shares one rung per role.</summary>
    [Fact]
    public void EveryRole_SitsAtOneLightnessAcrossTheAccents() {
        var roles = new (string Name, Func<AccentPreset, Color> Pick)[] {
            ("fill", a => a.Shades.Fill),
            ("hover", a => a.Shades.Hover),
            ("deep", a => a.Shades.Deep),
            ("light text", a => a.Text(dark: false).Fill),
            ("light text hover", a => a.Text(dark: false).Hover),
        };

        foreach (var (role, pick) in roles) {
            var levels = AccentPreset.All.Select(a => Tone.Lightness(pick(a))).ToList();

            Assert.True(levels.Max() - levels.Min() < 0.5,
                $"The {role} rung spans {levels.Max() - levels.Min():F1} L* across the four accents.");
        }
    }

    /// <summary>Blue is the reference the rungs were measured from, so it has to come back exactly as
    /// authored — the chart CPU series and the Settings "Default" swatch are the same color.</summary>
    [Fact]
    public void Blue_ReproducesTheAuthoredShadesExactly() {
        Assert.Equal(Color.Parse("#4cc2ff"), AccentPreset.Default.Shades.Fill);
        Assert.Equal(Color.Parse("#0078b6"), AccentPreset.Default.Text(dark: false).Fill);
    }

    /// <summary>Palette.axaml authors Blue's shades for the frame before <c>ThemeService</c> runs. A
    /// mismatch shows as the app starting in one blue and settling into another.</summary>
    [Fact]
    public void Palette_AuthorsBluesShadesForTheStartupFrame() {
        var blue = AccentPreset.Default;

        Assert.Equal(Hex(blue.Shades.Hover), Authored("AccentHover"));
        Assert.Equal(Hex(blue.Shades.OnAccent), Authored("OnAccent"));
        Assert.Equal(Hex(blue.Shades.Deep), Authored("AccentDeep"));
        Assert.Equal(Hex(blue.Shades.Fill), Authored("AccentColor"));
        Assert.Equal(Hex(blue.Text(dark: true).Hover), Authored("AccentTextHover"));
    }

    private static string Hex(Color c) => $"#{c.R:x2}{c.G:x2}{c.B:x2}";

    /// <summary>One top-level accent key's authored literal. The accent set is spelled both ways — a
    /// brush's attribute, and a bare Color element for the gradient stops.</summary>
    private static string Authored(string key) {
        var xaml = File.ReadAllText(Path.Combine(SourceRoot(), "src/Shared/Styles/Palette.axaml"));

        var match = Regex.Match(xaml, $"x:Key=\"{key}\"[^>]*?Color=\"(#[0-9A-Fa-f]{{6}})");
        if (!match.Success)
            match = Regex.Match(xaml, $"x:Key=\"{key}\">(#[0-9A-Fa-f]{{6}})<");

        Assert.True(match.Success, $"Palette.axaml has no literal colour for {key}.");
        return match.Groups[1].Value.ToLowerInvariant();
    }

    /// <summary>Walks up to the repository from this file's own compile-time path, as the other palette
    /// tests do: anchoring to the binaries breaks under <c>--artifacts-path</c>.</summary>
    private static string SourceRoot([CallerFilePath] string thisFile = "") {
        var dir = new DirectoryInfo(Path.GetDirectoryName(thisFile)!);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "DashDetective.sln")))
            dir = dir.Parent;

        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "DashDetective");
    }
}
