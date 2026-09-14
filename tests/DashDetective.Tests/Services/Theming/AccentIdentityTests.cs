using Avalonia.Media;
using DashDetective.Services.Theming;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Xunit;

namespace DashDetective.Tests.Services.Theming;

/// <summary>
/// Pins the default accent against the model: its identity is the ladder's fill rung, its graphic shades do
/// not follow the theme, and Palette.axaml authors the same values for the startup frame.
/// </summary>
public class AccentIdentityTests {
    private static AccentPreset Blue => AccentPreset.Default;

    /// <summary>The rungs were measured from Blue, so it has to sit on the fill rung it defines.</summary>
    [Fact]
    public void Default_SitsOnTheLaddersFillRung() {
        Assert.Equal(AccentTone.Fill, Tone.Lightness(Blue.Identity), 0.5);
    }

    /// <summary>What the bug asked for: the logo, the navigation highlight and the swatch are the same
    /// colour whichever theme is on. Only the text shade may differ.</summary>
    [Fact]
    public void GraphicShades_DoNotFollowTheTheme() {
        Assert.Equal(Blue.Identity, Blue.Shades.Fill);
        Assert.Equal(AccentTone.Graphic(Blue.Identity), Blue.Shades);
    }

    /// <summary>The bug this model replaces: a hue that turned up to 19 degrees and lost 48 points of
    /// saturation between themes.</summary>
    [Fact]
    public void EveryShade_KeepsItsIdentity() {
        var identity = Blue.Identity.ToHsl();

        Color[] shades = [
            Blue.Shades.Fill, Blue.Shades.Hover, Blue.Shades.Deep,
            Blue.Text(dark: true).Fill, Blue.Text(dark: true).Hover,
            Blue.Text(dark: false).Fill, Blue.Text(dark: false).Hover,
        ];

        foreach (var shade in shades) {
            Assert.Equal(identity.H, shade.ToHsl().H, 1.0);
            Assert.Equal(identity.S, shade.ToHsl().S, 0.02);
        }
    }

    /// <summary>Blue is the reference the rungs were measured from, so it has to come back exactly as
    /// authored — the chart CPU series and the Settings "Default" swatch are the same color.</summary>
    [Fact]
    public void Blue_ReproducesTheAuthoredShadesExactly() {
        Assert.Equal(Color.Parse("#4cc2ff"), Blue.Shades.Fill);
        Assert.Equal(Color.Parse("#0078b6"), Blue.Text(dark: false).Fill);
    }

    /// <summary>Palette.axaml authors Blue's shades for the frame before <c>ThemeService</c> runs. A
    /// mismatch shows as the app starting in one blue and settling into another.</summary>
    [Fact]
    public void Palette_AuthorsBluesShadesForTheStartupFrame() {
        Assert.Equal(Hex(Blue.Shades.Hover), Authored("AccentHover"));
        Assert.Equal(Hex(Blue.Shades.OnAccent), Authored("OnAccent"));
        Assert.Equal(Hex(Blue.Shades.Deep), Authored("AccentDeep"));
        Assert.Equal(Hex(Blue.Shades.Fill), Authored("AccentColor"));
        Assert.Equal(Hex(Blue.Text(dark: true).Hover), Authored("AccentTextHover"));
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
