using Avalonia.Media;
using DashDetective.Services.Theming;
using Xunit;

namespace DashDetective.Tests.Services.Theming;

/// <summary>
/// Covers how an accent is named and persisted: a preset colour resolves to the preset itself, anything
/// else is Custom, and a stored hex that cannot be read falls back to Blue rather than failing.
/// </summary>
public class AccentPresetTests {
    [Theory]
    [InlineData("#1a3a8a")]
    [InlineData("1a3a8a")]
    [InlineData("  #1A3A8A ")]
    public void TryParseHex_SixDigits_WithOrWithoutTheHash(string text) {
        Assert.True(AccentPreset.TryParseHex(text, out var color));
        Assert.Equal(Color.FromRgb(0x1a, 0x3a, 0x8a), color);
    }

    [Fact]
    public void TryParseHex_ThreeDigits_Expand() {
        Assert.True(AccentPreset.TryParseHex("#f80", out var color));
        Assert.Equal(Color.FromRgb(0xff, 0x88, 0x00), color);
    }

    /// <summary>Names, alpha and stray characters are refused, so a typed value persists as typed.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("red")]
    [InlineData("#ff1a3a8a")]
    [InlineData("#12 456")]
    [InlineData(" 12345")]
    [InlineData("##123456")]
    [InlineData("#gg0000")]
    public void TryParseHex_AnythingElse_IsRefused(string? text) {
        Assert.False(AccentPreset.TryParseHex(text, out _));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not a colour")]
    public void FromHex_Unreadable_FallsBackToBlue(string? text) {
        Assert.Same(AccentPreset.Default, AccentPreset.FromHex(text));
    }

    [Fact]
    public void FromHex_APresetsColour_ResolvesToThePreset() {
        foreach (var preset in AccentPreset.All)
            Assert.Same(preset, AccentPreset.FromHex(preset.Hex));
    }

    [Fact]
    public void FromIdentity_AnyOtherColour_IsCustomAndOpaque() {
        var accent = AccentPreset.FromIdentity(Color.FromArgb(0x80, 0x1a, 0x3a, 0x8a));

        Assert.Equal(AccentPreset.CustomName, accent.Name);
        Assert.Equal(Color.FromRgb(0x1a, 0x3a, 0x8a), accent.Identity);
        Assert.Equal("#1a3a8a", accent.Hex);
    }
}
