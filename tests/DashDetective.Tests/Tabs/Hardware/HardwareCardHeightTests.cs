using Avalonia;
using Avalonia.Controls;
using DashDetective.Shared.Layout;
using DashDetective.Tests.Services.Theming;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace DashDetective.Tests.Tabs.Hardware;

/// <summary>
/// Hardware cards in a row share one height, and the card holds no height of its own. The panel arranges
/// every card at its row's height, so the card must fill its slot (no <c>VerticalAlignment="Top"</c>, no
/// <c>Height</c>) and take only a floor from Dimensions.axaml; a fixed height would clip as soon as text
/// size grows.
/// </summary>
public class HardwareCardHeightTests {
    private static readonly string Src = Path.Combine(PaletteFile.SourceRoot(), "src");

    [Fact]
    public void Arrange_CardsThatFillTheirSlot_ShareTheTallestCardsHeightInTheRow() {
        var panel = new UniformFlowPanel { MinItemWidth = 100, ColumnSpacing = 10, RowSpacing = 10 };
        foreach (var size in new[] { 40.0, 90.0, 60.0, 30.0 })
            panel.Children.Add(new Border { Child = new Border { Height = size } });

        panel.Measure(new Size(210, double.PositiveInfinity));
        panel.Arrange(new Rect(0, 0, 210, panel.DesiredSize.Height));

        // 210 wide at a 100 minimum is two columns: {40, 90} then {60, 30}.
        Assert.Equal(new[] { 90.0, 90.0, 60.0, 60.0 }, panel.Children.Select(c => c.Bounds.Height));
    }

    [Fact]
    public void Card_SetsNoHeightAndDoesNotPinItselfToTheTop() {
        var card = CardBorder();

        Assert.Null(card.Attribute("Height"));
        Assert.Null(card.Attribute("MaxHeight"));
        Assert.Null(card.Attribute("VerticalAlignment"));
    }

    [Fact]
    public void Card_TakesItsFloorFromTheDimensionsToken() {
        var floor = Regex.Match(File.ReadAllText(Path.Combine(Src, "Shared/Styles/Dimensions.axaml")),
            """x:Key="HardwareCardMinHeight">([\d.]+)<""");

        Assert.True(floor.Success, "Dimensions.axaml must define HardwareCardMinHeight.");
        Assert.True(double.Parse(floor.Groups[1].Value, CultureInfo.InvariantCulture) > 0);
        Assert.Equal("{StaticResource HardwareCardMinHeight}", (string?)CardBorder().Attribute("MinHeight"));
    }

    private static XElement CardBorder() {
        var view = XDocument.Load(Path.Combine(Src, "Tabs/Hardware/HardwareView.axaml"));
        // The first template is the card; the spec-row template nests inside it.
        var template = view.Descendants().First(e => e.Name.LocalName == "DataTemplate");

        return template.Elements().Single(e => e.Name.LocalName == "Border");
    }
}
