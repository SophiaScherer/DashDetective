using Avalonia.Media;
using DashDetective.Services.Theming;
using Xunit;

namespace DashDetective.Tests.Services.Theming;

/// <summary>
/// Covers <see cref="AccentTone"/>, the rule an accent's shades follow: only lightness may vary, and it
/// varies to a stated CIE L* rather than by eye. The maths itself is <see cref="Tone"/>'s and is pinned
/// by <c>ToneTests</c>; what is checked here is that every accent shade comes off it keeping its identity.
/// </summary>
public class AccentToneTests {
    /// <summary>A saturated hue and a muted one, since HSL lightness treats the two differently and the
    /// ladder exists to stop that mattering.</summary>
    public static TheoryData<string> Identities() => ["#4cc2ff", "#6dcc61", "#d0a4ff", "#ff9f79"];

    /// <summary>The graphic fill is the identity itself, untouched — the rule's anchor.</summary>
    [Theory]
    [MemberData(nameof(Identities))]
    public void Graphic_TakesTheIdentityAsItsFill(string hex) {
        var identity = Color.Parse(hex);

        Assert.Equal(identity, AccentTone.Graphic(identity).Fill);
    }

    /// <summary>The dark theme draws the accent as text at the identity; only light darkens it.</summary>
    [Theory]
    [MemberData(nameof(Identities))]
    public void Text_Dark_IsTheIdentityAndLightIsDarker(string hex) {
        var identity = Color.Parse(hex);

        Assert.Equal(identity, AccentTone.Text(identity, dark: true).Fill);
        Assert.True(Tone.Lightness(AccentTone.Text(identity, dark: false).Fill)
                    < Tone.Lightness(identity));
    }

    /// <summary>Generalising the rule for a custom accent must not move a preset: each still takes the
    /// ladder's rungs exactly.</summary>
    [Theory]
    [MemberData(nameof(Identities))]
    public void Preset_TakesTheLaddersRungsExactly(string hex) {
        var identity = Color.Parse(hex);

        Assert.Equal(new AccentShades(identity,
                                      Tone.WithLightness(identity, AccentTone.Hover),
                                      Tone.WithLightness(identity, AccentTone.OnAccent),
                                      Tone.WithLightness(identity, AccentTone.Deep)),
                     AccentTone.Graphic(identity));
        Assert.Equal(new AccentTextShades(identity, Tone.WithLightness(identity, AccentTone.Hover)),
                     AccentTone.Text(identity, dark: true));
        Assert.Equal(new AccentTextShades(Tone.WithLightness(identity, Tone.LightText),
                                          Tone.WithLightness(identity, AccentTone.LightTextHover)),
                     AccentTone.Text(identity, dark: false));
    }

    /// <summary>A dark fill cannot carry dark text, so its label turns light and hover darkens away from it.</summary>
    [Fact]
    public void Graphic_DarkIdentity_TurnsTheLabelLightAndDarkensOnHover() {
        var identity = Color.Parse("#1a3a8a");
        var shades = AccentTone.Graphic(identity);

        Assert.True(Tone.Lightness(shades.OnAccent) > Tone.Lightness(identity));
        Assert.True(Tone.Lightness(shades.Hover) < Tone.Lightness(identity));
    }

    /// <summary>Near black, the deep stop turns lighter rather than vanishing.</summary>
    [Fact]
    public void Graphic_NearBlackIdentity_KeepsAVisibleGradient() {
        var identity = Color.Parse("#101018");
        var shades = AccentTone.Graphic(identity);

        Assert.True(Tone.Lightness(shades.Deep) - Tone.Lightness(identity) > 15);
    }

    /// <summary>At black and white a hover step would clamp back onto the fill; it has to stay visible.</summary>
    [Theory]
    [InlineData("#000000")]
    [InlineData("#000010")]
    [InlineData("#ffffff")]
    public void Hover_AtEitherEnd_StillDiffersFromTheFill(string hex) {
        var identity = Color.Parse(hex);
        var graphic = AccentTone.Graphic(identity);

        Assert.NotEqual(graphic.Fill, graphic.Hover);
        foreach (var dark in new[] { true, false }) {
            var text = AccentTone.Text(identity, dark);
            Assert.NotEqual(text.Fill, text.Hover);
        }
    }

    /// <summary>Every shade, graphic and text alike, keeps the identity it was derived from.</summary>
    [Theory]
    [MemberData(nameof(Identities))]
    public void EveryShade_KeepsTheIdentity(string hex) {
        var identity = Color.Parse(hex);
        var hsl = identity.ToHsl();
        var graphic = AccentTone.Graphic(identity);

        Color[] shades = [
            graphic.Fill, graphic.Hover, graphic.Deep, graphic.OnAccent,
            AccentTone.Text(identity, dark: true).Fill, AccentTone.Text(identity, dark: true).Hover,
            AccentTone.Text(identity, dark: false).Fill, AccentTone.Text(identity, dark: false).Hover,
        ];

        foreach (var shade in shades) {
            Assert.Equal(hsl.H, shade.ToHsl().H, 4.0);
            Assert.Equal(hsl.S, shade.ToHsl().S, 0.02);
        }
    }
}
