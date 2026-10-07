using DashDetective.Tests.Services.Theming;
using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace DashDetective.Tests.Shared.Styles;

/// <summary>
/// A modal card fits its window: it takes the shared <c>modalCard</c> class (margin and centering) and
/// never a fixed Width, which would overflow a window narrower than it. Help's card was 660 wide and
/// unbounded by the window, and its body ended off screen in a short one.
/// </summary>
public class ModalCardTests {
    private static readonly string Src = Path.Combine(PaletteFile.SourceRoot(), "src");

    [Fact]
    public void SharedStyle_MarginsTheCardInsideTheWindow() {
        var styles = XDocument.Load(Path.Combine(Src, "Shared/Styles/SharedStyles.axaml"));

        var style = Assert.Single(styles.Descendants(),
            e => e.Name.LocalName == "Style" && (string?)e.Attribute("Selector") == "Border.modalCard");

        Assert.Contains(style.Elements(), s => (string?)s.Attribute("Property") == "Margin");
    }

    [Fact]
    public void EveryOverlayCard_TakesTheSharedClassAndNoFixedWidth() {
        var cards = Directory.EnumerateFiles(Src, "*Overlay.axaml", SearchOption.AllDirectories)
            .Select(file => (file, card: Card(file)))
            .ToList();

        Assert.True(cards.Count >= 2, "Help and the accent picker are both modal overlays.");
        foreach (var (file, card) in cards) {
            var classes = ((string?)card.Attribute("Classes") ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
            Assert.True(classes.Contains("modalCard", StringComparer.Ordinal), $"{Path.GetFileName(file)}: Card needs modalCard.");
            Assert.Null(card.Attribute("Width"));
            Assert.Null(card.Attribute("Margin"));
        }
    }

    private static XElement Card(string file) =>
        Assert.Single(XDocument.Load(file).Descendants(),
            e => (string?)e.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == "Card");
}
