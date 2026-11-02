using Avalonia.Media;
using DashDetective.Services.Theming;
using DashDetective.Tests.Services.Theming;
using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace DashDetective.Tests.Shared.Controls;

/// <summary>
/// Pins <c>BrandMark</c> to the brand source in <c>docs/brand</c>: the same geometry, a fill that equals the
/// asset at the default accent, and the only place any view draws the mark. Markup reads, so no render
/// backend is needed.
/// </summary>
public class BrandMarkTests {
    private static readonly XNamespace Svg = "http://www.w3.org/2000/svg";

    private static string Source(string relative) => Path.Combine(PaletteFile.SourceRoot(), relative);

    private static XElement SourcePath() =>
        XDocument.Load(Path.Combine(PaletteFile.SourceRoot(), "..", "docs", "brand", "dashdetective-mark.svg"))
            .Descendants(Svg + "path").Single();

    private static XElement ControlPath() =>
        XDocument.Load(Source("src/Shared/Controls/BrandMark.axaml"))
            .Descendants().Single(e => e.Name.LocalName == "Path");

    [Fact]
    public void Geometry_MatchesTheBrandSource() {
        var geometry = ControlPath().Descendants().Single(e => e.Name.LocalName == "PathGeometry");

        Assert.Equal((string)SourcePath().Attribute("d")!, (string)geometry.Attribute("Figures")!);
        Assert.Equal("evenodd", (string)SourcePath().Attribute("fill-rule")!);
        Assert.Equal("EvenOdd", (string)geometry.Attribute("FillRule")!);
    }

    [Fact]
    public void Fill_IsTheDefaultAccentDeepShade() {
        var brush = ControlPath().Descendants().Single(e => e.Name.LocalName == "SolidColorBrush");

        Assert.Equal("{DynamicResource AccentDeep}", (string)brush.Attribute("Color")!);
        Assert.Equal(Color.Parse((string)SourcePath().Attribute("fill")!), AccentPreset.Default.Shades.Deep);
    }

    [Theory]
    [InlineData("src/Shared/Controls/TitleBar.axaml")]
    [InlineData("src/Shell/Navigation/NavigationView.axaml")]
    [InlineData("src/Tabs/Settings/AccentPreview.axaml")]
    public void Views_DrawTheMarkThroughTheSharedControl(string view) {
        var root = XDocument.Load(Source(view)).Root!;

        Assert.Single(root.Descendants(), e => e.Name.LocalName == "BrandMark");
    }

    [Fact]
    public void Source_CarriesNoCopyOfTheMarkGeometry() {
        var geometry = (string)SourcePath().Attribute("d")!;
        var control = Path.GetFullPath(Source("src/Shared/Controls/BrandMark.axaml"));

        var copies = Directory.EnumerateFiles(Source("src"), "*.axaml", SearchOption.AllDirectories)
            .Where(f => !string.Equals(Path.GetFullPath(f), control, StringComparison.Ordinal))
            .Where(f => File.ReadAllText(f).Contains(geometry, StringComparison.Ordinal))
            .ToList();

        Assert.True(copies.Count == 0, "Draws the mark without BrandMark: " + string.Join(", ", copies));
    }
}
