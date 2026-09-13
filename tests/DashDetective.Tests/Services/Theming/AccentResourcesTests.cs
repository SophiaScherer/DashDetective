using Avalonia.Controls;
using Avalonia.Media;
using DashDetective.Services.Theming;
using System.Linq;
using Xunit;

namespace DashDetective.Tests.Services.Theming;

/// <summary>
/// Covers <see cref="AccentResources"/>, the one list of accent keys the app and the Settings preview both
/// write. A key added to Palette.axaml but not here would stay Blue whatever was picked.
/// </summary>
public class AccentResourcesTests {
    [Fact]
    public void Write_CoversEveryAccentKeyThePaletteDeclares() {
        var resources = new ResourceDictionary();
        AccentResources.Write(resources, AccentPreset.Default, dark: true);

        var declared = PaletteFile.TopLevel.Where(key => key.Contains("Accent")).ToList();

        Assert.NotEmpty(declared);
        foreach (var key in declared.Append("AccentColor").Append("AccentDeep"))
            Assert.True(resources.ContainsKey(key), $"{key} is not written.");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Write_TakesTheShadesAndTheThemesTextPair(bool dark) {
        var accent = AccentPreset.FromHex("#1a3a8a");
        var resources = new ResourceDictionary();

        AccentResources.Write(resources, accent, dark);

        Assert.Equal(accent.Shades.Fill, ((SolidColorBrush)resources["Accent"]!).Color);
        Assert.Equal(accent.Shades.OnAccent, ((SolidColorBrush)resources["OnAccent"]!).Color);
        Assert.Equal(accent.Shades.Deep, (Color)resources["AccentDeep"]!);
        Assert.Equal(accent.Text(dark).Fill, ((SolidColorBrush)resources["AccentText"]!).Color);
    }
}
