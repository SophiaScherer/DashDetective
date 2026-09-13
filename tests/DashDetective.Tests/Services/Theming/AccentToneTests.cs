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
