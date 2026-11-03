using Avalonia.Automation;
using Avalonia.Controls;
using DashDetective.Shared.Controls;
using DashDetective.Shell.Navigation;
using DashDetective.Tests.Services.Theming;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace DashDetective.Tests.Shell.Navigation;

/// <summary>Covers which widgets become sections in the nav bar's list and in what order. A section is a
/// visible widget with a title, listed in reading order of where it was ARRANGED — rows top to bottom,
/// left to right within a row — since a dragged layout no longer matches the markup. The list is offered
/// only when there are at least two to jump between. A card that is not a widget, such as a Hardware card,
/// is a section when it carries <see cref="PageSections.SectionClass"/>, titled by its automation name.</summary>
public class PageSectionsTests {
    private static SectionCandidate At(string? title, double top, double left, bool visible = true) =>
        new(title, top, left, visible);

    private static string[] Titles(params SectionCandidate[] candidates) =>
        PageSections.Discover(candidates).Select(s => s.Title).ToArray();

    [Fact]
    public void Discover_SingleColumn_ListsTopToBottom() {
        Assert.Equal(["Appearance", "Monitoring", "Keyboard"],
            Titles(At("Keyboard", 600, 0), At("Appearance", 0, 0), At("Monitoring", 300, 0)));
    }

    [Fact]
    public void Discover_SharedRow_ListsLeftToRight() {
        Assert.Equal(["CPU", "Memory", "Network"],
            Titles(At("Memory", 0, 400), At("Network", 300, 0), At("CPU", 0, 0)));
    }

    // A reordered board arranges the dragged widget first even though the markup declares it last.
    [Fact]
    public void Discover_FollowsArrangedOrderNotDeclarationOrder() {
        Assert.Equal(["Ping", "Adapters"], Titles(At("Adapters", 320, 0), At("Ping", 0, 0)));
    }

    [Fact]
    public void Discover_TopsWithinTheTolerance_ReadAsOneRow() {
        var nearlyLevel = PageSections.RowTolerance - 1;

        Assert.Equal(["Left", "Right"], Titles(At("Right", 0, 500), At("Left", nearlyLevel, 0)));
    }

    [Fact]
    public void Discover_TopsBeyondTheTolerance_ReadAsSeparateRows() {
        var lower = PageSections.RowTolerance + 1;

        Assert.Equal(["Upper", "Lower"], Titles(At("Lower", lower, 0), At("Upper", 0, 500)));
    }

    [Fact]
    public void Discover_SkipsUntitledAndHiddenWidgets() {
        Assert.Equal(["Shown"],
            Titles(At(null, 0, 0), At("  ", 10, 0), At("Hidden", 20, 0, visible: false), At("Shown", 30, 0)));
    }

    // A widget that could not be placed relative to the page has no position to sort by.
    [Fact]
    public void Discover_SkipsWidgetsWithNoPosition() {
        Assert.Equal(["Placed"], Titles(At("Lost", double.NaN, double.NaN), At("Placed", 0, 0)));
    }

    [Fact]
    public void Discover_TrimsTitles() {
        Assert.Equal(["Alerts"], Titles(At("  Alerts ", 0, 0)));
    }

    // The source index is how the view finds the widget again, so it must name the candidate, not the slot.
    [Fact]
    public void Discover_SourceNamesTheCandidatePosition() {
        var sections = PageSections.Discover([At(null, 0, 0), At("B", 50, 0), At("A", 10, 0)]);

        Assert.Equal([2, 1], sections.Select(s => s.Source));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(9, true)]
    public void ShouldShow_NeedsAtLeastTwo(int count, bool expected) {
        Assert.Equal(expected, PageSections.ShouldShow(count));
    }

    [Fact]
    public void IsSection_WidgetPanel_IsASectionTitledOrNot() {
        Assert.True(PageSections.IsSection(new WidgetPanel()));
        Assert.True(PageSections.IsSection(new WidgetPanel { Title = "Alerts" }));
    }

    [Fact]
    public void IsSection_MarkedCard_IsASection() {
        var card = new Border();
        card.Classes.Add(PageSections.SectionClass);

        Assert.True(PageSections.IsSection(card));
    }

    // A plain panel is surface styling, not a section: the Processes table and File Explorer panes use it.
    [Fact]
    public void IsSection_UnmarkedCard_IsNotASection() {
        var card = new Border();
        card.Classes.Add("panel");

        Assert.False(PageSections.IsSection(card));
    }

    [Fact]
    public void TitleOf_WidgetPanel_IsItsTitle() {
        Assert.Equal("Alerts", PageSections.TitleOf(new WidgetPanel { Title = "Alerts" }));
    }

    [Fact]
    public void TitleOf_MarkedCard_IsItsAutomationName() {
        var card = new Border();
        card.Classes.Add(PageSections.SectionClass);
        AutomationProperties.SetName(card, "Processor");

        Assert.Equal("Processor", PageSections.TitleOf(card));
    }

    // The Hardware page has no WidgetPanels, so without the marker it offers no list at all.
    [Fact]
    public void HardwareCard_CarriesTheSectionMarkerAndANameAndAFlash() {
        var view = XDocument.Load(Path.Combine(PaletteFile.SourceRoot(), "src", "Tabs", "Hardware", "HardwareView.axaml"));
        var card = view.Descendants().First(e => e.Name.LocalName == "DataTemplate")
            .Elements().Single(e => e.Name.LocalName == "Border");
        var classes = ((string?)card.Attribute("Classes") ?? "").Split(' ');

        Assert.Contains(PageSections.SectionClass, classes);
        Assert.Contains("revealFlash", classes);
        Assert.Equal("{Binding Title}", (string?)card.Attribute(XName.Get("AutomationProperties.Name")));
    }
}
