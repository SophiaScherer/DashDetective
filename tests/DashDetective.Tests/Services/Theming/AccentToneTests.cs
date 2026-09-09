using Avalonia.Media;
using DashDetective.Services.Theming;
using Xunit;

namespace DashDetective.Tests.Services.Theming;

/// <summary>
/// Covers <see cref="AccentTone"/>, the rule an accent's shades follow: only lightness may vary, and it
/// varies to a stated CIE L* rather than by eye. A shade that kept its hue but drifted in saturation
/// would look like a different colour while passing a hue-only check, so both are pinned here.
/// </summary>
public class AccentToneTests {
    /// <summary>A saturated hue and a muted one, since HSL lightness treats the two differently and the
    /// ladder exists to stop that mattering.</summary>
    public static TheoryData<string> Identities() => ["#4cc2ff", "#6dcc61", "#d0a4ff", "#ff9f79"];

    private static readonly double[] Rungs = [AccentTone.Hover, AccentTone.Deep, AccentTone.LightText];

    [Theory]
    [MemberData(nameof(Identities))]
    public void WithLightness_KeepsTheHueAndSaturation(string hex) {
        var identity = Color.Parse(hex).ToHsl();

        foreach (var rung in Rungs) {
            var shade = AccentTone.WithLightness(Color.Parse(hex), rung).ToHsl();

            Assert.Equal(identity.H, shade.H, 1.0);
            Assert.Equal(identity.S, shade.S, 0.02);
        }
    }

    [Theory]
    [MemberData(nameof(Identities))]
    public void WithLightness_LandsOnTheRequestedLightness(string hex) {
        foreach (var rung in Rungs) {
            var shade = AccentTone.WithLightness(Color.Parse(hex), rung);

            Assert.Equal(rung, AccentTone.Lightness(shade), 0.5);
        }
    }

    /// <summary>Asking for a colour's own lightness has to hand it straight back. This is what lets the
    /// authored identity double as the fill without a round-trip quietly shifting it.</summary>
    [Theory]
    [MemberData(nameof(Identities))]
    public void WithLightness_ItsOwnLightness_ReturnsTheColourUnchanged(string hex) {
        var identity = Color.Parse(hex);

        Assert.Equal(identity, AccentTone.WithLightness(identity, AccentTone.Lightness(identity)));
    }

    /// <summary>L* is anchored at both ends, so a rung is a number anyone can check rather than a
    /// house scale.</summary>
    [Fact]
    public void Lightness_BlackAndWhite_SitAtTheEndsOfTheScale() {
        Assert.Equal(0.0, AccentTone.Lightness(Colors.Black), 1);
        Assert.Equal(100.0, AccentTone.Lightness(Colors.White), 1);
    }

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
        Assert.True(AccentTone.Lightness(AccentTone.Text(identity, dark: false).Fill)
                    < AccentTone.Lightness(identity));
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
