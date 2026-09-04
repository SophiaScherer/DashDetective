using DashDetective.Services.Theming;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace DashDetective.Tests.Services.Theming;

/// <summary>
/// Pins that every accent is legible in both themes. The app draws real values in accent-coloured text —
/// a stat card's figure, "18.9 / 31 GB" — so the accent is body text, not decoration.
/// </summary>
public class AccentContrastTests {
    /// <summary>The surface an accent-coloured figure is drawn on in each theme.</summary>
    private static (int R, int G, int B) Surface(bool dark) => dark ? (20, 20, 20) : (255, 255, 255);

    public static TheoryData<string, bool> Cases() {
        var data = new TheoryData<string, bool>();
        foreach (var preset in AccentPreset.All)
            foreach (var dark in new[] { true, false })
                data.Add(preset.Name, dark);
        return data;
    }

    private static AccentPreset Preset(string name) =>
        AccentPreset.All.First(a => a.Name == name);

    /// <summary>Accent-coloured text on the page background. This is the one that failed before the light
    /// shades existed: every accent scored about 2:1 on white.</summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void AccentText_MeetsAaOnItsTheme(string name, bool dark) {
        var shades = Preset(name).For(dark);
        var ratio = ContrastRatio.Of(Rgb(shades.Fill), 1.0, Surface(dark));

        Assert.True(ratio >= ContrastRatio.AA,
            $"{name} on the {(dark ? "dark" : "light")} theme reads at {ratio:F2}:1 as text.");
    }

    /// <summary>Text drawn on the accent fill — a selected segment's label, the primary button.</summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void TextOnAccent_MeetsAaAgainstTheFill(string name, bool dark) {
        var shades = Preset(name).For(dark);
        var ratio = ContrastRatio.Of(Rgb(shades.OnAccent), 1.0, Rgb(shades.Fill));

        Assert.True(ratio >= ContrastRatio.AA,
            $"{name}'s on-accent text reads at {ratio:F2}:1 on its own fill ({(dark ? "dark" : "light")}).");
    }

    /// <summary>The pointer-over fill still has to carry the same text.</summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void TextOnAccent_MeetsAaAgainstTheHoverFill(string name, bool dark) {
        var shades = Preset(name).For(dark);
        var ratio = ContrastRatio.Of(Rgb(shades.OnAccent), 1.0, Rgb(shades.Hover));

        Assert.True(ratio >= ContrastRatio.AA,
            $"{name}'s on-accent text reads at {ratio:F2}:1 on its hover fill ({(dark ? "dark" : "light")}).");
    }

    /// <summary>Each accent stays its own choice: two presets rendering the same colour would make the
    /// picker offer a duplicate.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void EveryAccent_IsDistinctFromTheOthers(bool dark) {
        var fills = AccentPreset.All.Select(a => a.For(dark).Fill).ToList();

        Assert.Equal(fills.Count, fills.Distinct().Count());
    }

    private static (int R, int G, int B) Rgb(Avalonia.Media.Color c) => (c.R, c.G, c.B);
}
