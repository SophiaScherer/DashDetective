using DashDetective.Tabs.Toolkit;
using System.Linq;
using Xunit;

namespace DashDetective.Tests.Tabs.Toolkit;

/// <summary>Covers <see cref="ToolkitFilter"/>: the chip and the search box narrow the list together
/// (never one at the expense of the other), the term reaches both the command and its description, and
/// grouping keeps catalog order while dropping the sections a filter emptied.</summary>
public class ToolkitFilterTests {
    private static ToolkitEntry Entry(
        string command, string description = "",
        ToolkitCategory category = ToolkitCategory.SystemTools,
        ToolkitEntryKind kind = ToolkitEntryKind.Command) =>
        new(command, description, category, kind);

    [Fact]
    public void Matches_BlankTermAndNoCategory_KeepsEverything() {
        Assert.True(ToolkitFilter.Matches(Entry("anything"), null, null));
        Assert.True(ToolkitFilter.Matches(Entry("anything"), null, "   "));
    }

    [Fact]
    public void Matches_Term_IsCaseInsensitiveAcrossCommandAndDescription() {
        var entry = Entry("SomeCommand", "Opens the widget folder");

        Assert.True(ToolkitFilter.Matches(entry, null, "somecomm"));
        Assert.True(ToolkitFilter.Matches(entry, null, "WIDGET"));
        Assert.False(ToolkitFilter.Matches(entry, null, "gadget"));
    }

    [Fact]
    public void Matches_TermIsTrimmedBeforeComparing() {
        Assert.True(ToolkitFilter.Matches(Entry("cleanup"), null, "  clean  "));
    }

    [Fact]
    public void Matches_CategoryAndTerm_MustBothHold() {
        var entry = Entry("cleanup", category: ToolkitCategory.Maintenance);

        Assert.True(ToolkitFilter.Matches(entry, ToolkitCategory.Maintenance, "clean"));
        Assert.False(ToolkitFilter.Matches(entry, ToolkitCategory.Terminal, "clean"));
        Assert.False(ToolkitFilter.Matches(entry, ToolkitCategory.Maintenance, "nothing"));
    }

    [Fact]
    public void Group_OrdersSectionsByTheCatalogNotTheEntryList() {
        var entries = new[] {
            Entry("c", category: ToolkitCategory.Maintenance),
            Entry("a", category: ToolkitCategory.FileLocations),
            Entry("b", category: ToolkitCategory.Terminal),
        };

        var groups = ToolkitFilter.Group(entries, null, null);

        Assert.Equal(
            [ToolkitCategory.FileLocations, ToolkitCategory.Terminal, ToolkitCategory.Maintenance],
            groups.Select(g => g.Category));
    }

    [Fact]
    public void Group_DropsASectionTheFilterEmptied() {
        var entries = new[] {
            Entry("keep", category: ToolkitCategory.Terminal),
            Entry("drop", category: ToolkitCategory.Maintenance),
        };

        var groups = ToolkitFilter.Group(entries, null, "keep");

        Assert.Equal(ToolkitCategory.Terminal, Assert.Single(groups).Category);
    }

    [Fact]
    public void Group_KeepsEntryOrderWithinASection() {
        var entries = new[] { Entry("first"), Entry("second"), Entry("third") };

        var group = Assert.Single(ToolkitFilter.Group(entries, null, null));

        Assert.Equal(["first", "second", "third"], group.Items.Select(e => e.Command));
    }

    [Fact]
    public void Group_NoEntries_ReturnsNoSections() {
        Assert.Empty(ToolkitFilter.Group(ToolkitCatalog.Entries, null, null));
    }
}
