using DashDetective.Services.Theming;
using Xunit;

namespace DashDetective.Tests.Services.Theming;

/// <summary>
/// Pins that any accent is legible where it is read. The app draws real values in accent-colored text —
/// a stat card's figure, "18.9 / 31 GB" — so the text shade is body text, not decoration; the graphic
/// shades are one set for both themes and are measured against their own fill.
/// </summary>
public class AccentContrastTests {
    /// <summary>The surface an accent-colored figure is drawn on in each theme.</summary>
    private static (int R, int G, int B) Surface(bool dark) => dark ? (20, 20, 20) : (255, 255, 255);

    /// <summary>The cost of keeping one graphic shade for both themes, recorded rather than fixed: the default
    /// accent's fill or border on a white surface lands at 2.0:1, under WCAG's 3:1 for a graphic that carries
    /// meaning — the navigation highlight bar and the selected-widget border. Asserted as a band so that
    /// raising it is a deliberate change, not a silent one. High contrast is the answer offered today, as it
    /// is for the text ramp.</summary>
    [Fact]
    public void DefaultGraphic_OnWhite_RecordsThatItIsBelowTheNonTextBar() {
        var ratio = ContrastRatio.Of(Rgb(AccentPreset.Default.Shades.Fill), 1.0, Surface(dark: false));

        Assert.InRange(ratio, 1.95, 2.10);
    }

    /// <summary>A grid across the whole RGB cube plus a fine gray ramp, which holds the black/white
    /// crossover where on-accent text is hardest to place, plus the default itself.</summary>
    public static TheoryData<string> AnyColor() {
        var data = new TheoryData<string>();
        byte[] steps = [0, 51, 102, 153, 204, 255];
        foreach (var r in steps)
            foreach (var g in steps)
                foreach (var b in steps)
                    data.Add($"#{r:x2}{g:x2}{b:x2}");
        for (var v = 0; v <= 255; v += 5)
            data.Add($"#{v:x2}{v:x2}{v:x2}");
        data.Add(AccentPreset.Default.Hex);
        return data;
    }

    /// <summary>The label on a button or selected segment stays readable whatever colour is picked.</summary>
    [Theory]
    [MemberData(nameof(AnyColor))]
    public void AnyPick_TextOnAccent_MeetsAaOnFillAndHover(string hex) {
        var shades = AccentPreset.FromHex(hex).Shades;

        Assert.True(ContrastRatio.Of(Rgb(shades.OnAccent), 1.0, Rgb(shades.Fill)) >= ContrastRatio.AA,
                    $"{hex}: on-accent text on its fill.");
        Assert.True(ContrastRatio.Of(Rgb(shades.OnAccent), 1.0, Rgb(shades.Hover)) >= ContrastRatio.AA,
                    $"{hex}: on-accent text on its hover fill.");
    }

    /// <summary>Accent text is corrected per theme: on white for light, and on every dark surface
    /// for dark.</summary>
    [Theory]
    [MemberData(nameof(AnyColor))]
    public void AnyPick_AccentText_MeetsAaOnItsTheme(string hex) {
        var accent = AccentPreset.FromHex(hex);

        foreach (var shade in new[] { accent.Text(dark: false).Fill, accent.Text(dark: false).Hover })
            Assert.True(ContrastRatio.Of(Rgb(shade), 1.0, Surface(dark: false)) >= ContrastRatio.AA,
                        $"{hex}: light text {shade}.");

        foreach (var surface in AccentGuard.DarkSurfaces)
            foreach (var shade in new[] { accent.Text(dark: true).Fill, accent.Text(dark: true).Hover })
                Assert.True(ContrastRatio.Of(Rgb(shade), 1.0, Rgb(surface)) >= ContrastRatio.AA,
                            $"{hex}: dark text {shade} on {surface}.");
    }

    private static (int R, int G, int B) Rgb(Avalonia.Media.Color c) => (c.R, c.G, c.B);
}
