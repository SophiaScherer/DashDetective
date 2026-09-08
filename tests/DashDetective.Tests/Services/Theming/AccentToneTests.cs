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

    [Theory]
    [MemberData(nameof(Identities))]
    public void WithLightness_KeepsTheHueAndSaturation(string hex) {
        var identity = Color.Parse(hex).ToHsl();

        foreach (var target in new[] { AccentTone.DarkHover, AccentTone.DarkDeep, AccentTone.LightFill }) {
            var shade = AccentTone.WithLightness(Color.Parse(hex), target).ToHsl();

            Assert.Equal(identity.H, shade.H, 1.0);
            Assert.Equal(identity.S, shade.S, 0.02);
        }
    }

    [Theory]
    [MemberData(nameof(Identities))]
    public void WithLightness_LandsOnTheRequestedLightness(string hex) {
        foreach (var target in new[] { AccentTone.DarkHover, AccentTone.DarkDeep, AccentTone.LightFill }) {
            var shade = AccentTone.WithLightness(Color.Parse(hex), target);

            Assert.Equal(target, AccentTone.Lightness(shade), 0.5);
        }
    }

    /// <summary>Asking for a colour's own lightness has to hand it straight back. This is what lets the
    /// authored identity double as the dark fill without a round-trip quietly shifting it.</summary>
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

    /// <summary>The dark fill is the identity itself, untouched — the rule's anchor.</summary>
    [Theory]
    [MemberData(nameof(Identities))]
    public void Shades_Dark_TakesTheIdentityAsItsFill(string hex) {
        var identity = Color.Parse(hex);

        Assert.Equal(identity, AccentTone.Shades(identity, dark: true).Fill);
    }

    /// <summary>On-accent text is the stated exception: read against the fill rather than beside it, so
    /// it is chosen for contrast and leaves the hue family.</summary>
    [Theory]
    [MemberData(nameof(Identities))]
    public void Shades_Light_DrawsOnAccentTextInWhite(string hex) {
        Assert.Equal(Colors.White, AccentTone.Shades(Color.Parse(hex), dark: false).OnAccent);
    }

    /// <summary>The whole point, on the shades a reader sees side by side: fill, hover and deep keep the
    /// identity in both themes.</summary>
    [Theory]
    [MemberData(nameof(Identities))]
    public void Shades_FillHoverAndDeep_KeepTheIdentityInBothThemes(string hex) {
        var identity = Color.Parse(hex).ToHsl();

        foreach (var dark in new[] { true, false }) {
            var shades = AccentTone.Shades(Color.Parse(hex), dark);

            foreach (var shade in new[] { shades.Fill, shades.Hover, shades.Deep }) {
                Assert.Equal(identity.H, shade.ToHsl().H, 1.0);
                Assert.Equal(identity.S, shade.ToHsl().S, 0.02);
            }
        }
    }
}
