using Avalonia.Media;
using DashDetective.Services.Theming;
using Xunit;

namespace DashDetective.Tests.Services.Theming;

/// <summary>
/// Covers <see cref="AccentGuard"/>: its surfaces are the ones Palette.axaml ships, the default is not flagged,
/// and a fill that all but disappears on a theme is.
/// </summary>
public class AccentGuardTests {
    private static readonly string[] SurfaceKeys = ["AppBackground", "SidebarBackground", "PanelBackground", "CardBackground", "FieldBackground"];

    [Theory]
    [InlineData("Dark", true)]
    [InlineData("Light", false)]
    public void Surfaces_MirrorThePalette(string variant, bool dark) {
        var mirror = dark ? AccentGuard.DarkSurfaces : AccentGuard.LightSurfaces;

        Assert.Equal(SurfaceKeys.Length, mirror.Length);
        for (var i = 0; i < SurfaceKeys.Length; i++) {
            var (r, g, b) = PaletteFile.Tables[variant][SurfaceKeys[i]].Color;
            Assert.Equal(Color.FromRgb((byte)r, (byte)g, (byte)b), mirror[i]);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Default_IsNeverFlagged(bool dark) {
        Assert.False(AccentGuard.IsFaint(AccentPreset.Default.Shades.Fill, dark));
    }

    [Fact]
    public void PaleFill_IsFlaggedOnLightOnly() {
        var pale = Color.Parse("#fff3b0");

        Assert.True(AccentGuard.IsFaint(pale, dark: false));
        Assert.False(AccentGuard.IsFaint(pale, dark: true));
    }

    [Fact]
    public void DarkFill_IsFlaggedOnDarkOnly() {
        var navy = Color.Parse("#141a30");

        Assert.True(AccentGuard.IsFaint(navy, dark: true));
        Assert.False(AccentGuard.IsFaint(navy, dark: false));
    }
}
