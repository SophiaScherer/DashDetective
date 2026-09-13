using Avalonia.Media;
using DashDetective.Services.Theming;
using Xunit;

namespace DashDetective.Tests.Services.Theming;

/// <summary>
/// Covers <see cref="Tone"/>, the lightness maths the accent, the text ramp and the chart series all
/// measure against. A shade that kept its hue but drifted in saturation would look like a different
/// colour while passing a hue-only check, so both are pinned here.
/// </summary>
public class ToneTests {
    /// <summary>A saturated hue and a muted one, since HSL lightness treats the two differently and the
    /// ladder exists to stop that mattering.</summary>
    public static TheoryData<string> Identities() => ["#4cc2ff", "#6dcc61", "#d0a4ff", "#ff9f79"];

    /// <summary>Rungs spread across the scale, rather than any one caller's ladder.</summary>
    private static readonly double[] Rungs = [79.1, 52.3, 48.0, 14.0];

    [Theory]
    [MemberData(nameof(Identities))]
    public void WithLightness_KeepsTheHueAndSaturation(string hex) {
        var identity = Color.Parse(hex).ToHsl();

        foreach (var rung in Rungs) {
            var shade = Tone.WithLightness(Color.Parse(hex), rung).ToHsl();

            Assert.Equal(identity.H, shade.H, 1.0);
            Assert.Equal(identity.S, shade.S, 0.02);
        }
    }

    [Theory]
    [MemberData(nameof(Identities))]
    public void WithLightness_LandsOnTheRequestedLightness(string hex) {
        foreach (var rung in Rungs) {
            var shade = Tone.WithLightness(Color.Parse(hex), rung);

            Assert.Equal(rung, Tone.Lightness(shade), 0.5);
        }
    }

    /// <summary>Asking for a colour's own lightness has to hand it straight back. This is what lets an
    /// authored identity double as the fill without a round-trip quietly shifting it.</summary>
    [Theory]
    [MemberData(nameof(Identities))]
    public void WithLightness_ItsOwnLightness_ReturnsTheColourUnchanged(string hex) {
        var identity = Color.Parse(hex);

        Assert.Equal(identity, Tone.WithLightness(identity, Tone.Lightness(identity)));
    }

    /// <summary>L* is anchored at both ends, so a rung is a number anyone can check rather than a
    /// house scale.</summary>
    [Fact]
    public void Lightness_BlackAndWhite_SitAtTheEndsOfTheScale() {
        Assert.Equal(0.0, Tone.Lightness(Colors.Black), 1);
        Assert.Equal(100.0, Tone.Lightness(Colors.White), 1);
    }

    /// <summary>The ends of the alpha range, where compositing has to be a no-op either way. Opaque black
    /// on white is black; fully transparent black on white is white.</summary>
    [Fact]
    public void CompositedLightness_TheAlphaEnds_AreTheTwoColoursThemselves() {
        Assert.Equal(0.0, Tone.CompositedLightness(Colors.Black, 1.0, Colors.White), 1);
        Assert.Equal(100.0, Tone.CompositedLightness(Colors.Black, 0.0, Colors.White), 1);
    }

    /// <summary>The text ramp is opacity over a surface, not a colour, so this has to weigh what the
    /// renderer actually draws. Black at 50% on white composites to #808080, whose L* is 53.6 — read as
    /// the brush's own colour it would weigh 0 and every ramp assertion built on it would be wrong.</summary>
    [Fact]
    public void CompositedLightness_WeighsTheCompositeRatherThanTheBrush() {
        Assert.Equal(53.6, Tone.CompositedLightness(Colors.Black, 0.5, Colors.White), 1);
    }

    /// <summary>A heavier alpha has to read as a darker rung on a light surface, which is the ordering
    /// the ramp's monotonicity is checked against.</summary>
    [Fact]
    public void CompositedLightness_FallsAsTheAlphaRises() {
        var lighter = Tone.CompositedLightness(Colors.Black, 0.45, Colors.White);
        var heavier = Tone.CompositedLightness(Colors.Black, 0.95, Colors.White);

        Assert.True(heavier < lighter);
    }
}
