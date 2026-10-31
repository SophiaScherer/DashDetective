using DashDetective.Tests.Services.Theming;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace DashDetective.Tests.Shell.Search;

/// <summary>
/// Pins the search dropdown's popup binding. Avalonia's <c>Popup.IsOpen</c> is OneWay by default, so a popup
/// that closes itself never tells the view model, which then treats every later open as a no-op.
/// </summary>
public class UniversalSearchViewTests {
    [Fact]
    public void PopupIsOpen_BindsTwoWay_SoASelfCloseReachesTheViewModel() {
        var path = Path.Combine(PaletteFile.SourceRoot(), "src", "Shell", "Search", "UniversalSearchView.axaml");
        var popup = XDocument.Load(path).Descendants().Single(e => e.Name.LocalName == "Popup");

        var isOpen = popup.Attribute("IsOpen")?.Value ?? "";

        Assert.Contains("Binding IsOpen", isOpen);
        Assert.Contains("Mode=TwoWay", isOpen);
    }
}
