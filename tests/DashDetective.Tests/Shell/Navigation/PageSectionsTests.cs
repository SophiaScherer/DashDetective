using DashDetective.Shell.Navigation;
using System.Linq;
using Xunit;

namespace DashDetective.Tests.Shell.Navigation;

/// <summary>Covers which widgets become sections in the nav bar's list and in what order. A section is a
/// visible widget with a title, listed in reading order of where it was ARRANGED — rows top to bottom,
/// left to right within a row — since a dragged layout no longer matches the markup. The list is offered
/// only when there are at least two to jump between.</summary>
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
}
