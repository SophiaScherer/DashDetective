using DashDetective.Shell.Navigation;
using DashDetective.Tests.Services.Theming;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace DashDetective.Tests.Tabs.Hardware;

/// <summary>Each Hardware card carries the section marker, an automation name and <c>revealFlash</c>; without the marker the page offers no list.</summary>
public class HardwareSectionTests {
    [Fact]
    public void Card_CarriesTheSectionMarkerANameAndAFlash() {
        var view = XDocument.Load(Path.Combine(PaletteFile.SourceRoot(), "src", "Tabs", "Hardware", "HardwareView.axaml"));
        // The first template is the card; the spec-row template nests inside it.
        var card = view.Descendants().First(e => e.Name.LocalName == "DataTemplate")
            .Elements().Single(e => e.Name.LocalName == "Border");
        var classes = ((string?)card.Attribute("Classes") ?? "").Split(' ');

        Assert.Contains(PageSections.SectionClass, classes);
        Assert.Contains("revealFlash", classes);
        Assert.Equal("{Binding Title}", (string?)card.Attribute(XName.Get("AutomationProperties.Name")));
    }
}
