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
        new(command, description, category, kind, ToolkitAction.Launch(command));

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
        var entry = Entry("cleanup", category: ToolkitCategory.DocsAndLinks);

        Assert.True(ToolkitFilter.Matches(entry, ToolkitCategory.DocsAndLinks, "clean"));
        Assert.False(ToolkitFilter.Matches(entry, ToolkitCategory.Diagnostics, "clean"));
        Assert.False(ToolkitFilter.Matches(entry, ToolkitCategory.DocsAndLinks, "nothing"));
    }

    [Fact]
    public void Group_OrdersSectionsByTheCatalogNotTheEntryList() {
        var entries = new[] {
            Entry("c", category: ToolkitCategory.DocsAndLinks),
            Entry("a", category: ToolkitCategory.Folders),
            Entry("b", category: ToolkitCategory.Diagnostics),
        };

        var groups = ToolkitFilter.Group(entries, null, null);

        Assert.Equal<ToolkitCategory?>(
            [ToolkitCategory.Folders, ToolkitCategory.Diagnostics, ToolkitCategory.DocsAndLinks],
            groups.Select(g => g.Category));
    }

    [Fact]
    public void Group_DropsASectionTheFilterEmptied() {
        var entries = new[] {
            Entry("keep", category: ToolkitCategory.Diagnostics),
            Entry("drop", category: ToolkitCategory.DocsAndLinks),
        };

        var groups = ToolkitFilter.Group(entries, null, "keep");

        Assert.Equal(ToolkitCategory.Diagnostics, Assert.Single(groups).Category!.Value);
    }

    // ----- Pinned section -----

    private static ToolkitEntry Pinned(
        string command, ToolkitCategory category = ToolkitCategory.SystemTools) {
        var entry = Entry(command, category: category);
        entry.IsPinned = true;
        return entry;
    }

    [Fact]
    public void Group_NothingPinned_HasNoPinnedSection() {
        var groups = ToolkitFilter.Group([Entry("a"), Entry("b")], null, null);

        Assert.DoesNotContain(groups, g => g.Header == ToolkitGroup.PinnedHeader);
    }

    [Fact]
    public void Group_PinnedEntries_LeadTheListInTheirOwnSection() {
        var groups = ToolkitFilter.Group([Entry("plain"), Pinned("starred")], null, null);

        Assert.Equal(ToolkitGroup.PinnedHeader, groups[0].Header);
        Assert.Null(groups[0].Category);
        Assert.Equal(["starred"], groups[0].Items.Select(e => e.Command));
    }

    /// <summary>Lifted, not copied: the same command must not appear in two sections, or the search
    /// reveal — which flashes the first row carrying it — could only ever find one of them.</summary>
    [Fact]
    public void Group_APinnedEntry_LeavesItsCategorySectionRatherThanAppearingTwice() {
        var groups = ToolkitFilter.Group(
            [Pinned("starred", ToolkitCategory.Folders), Entry("plain", category: ToolkitCategory.Folders)],
            null, null);

        var everything = groups.SelectMany(g => g.Items).Select(e => e.Command).ToList();

        Assert.Equal(["starred", "plain"], everything);
        Assert.Single(everything, c => c == "starred");
    }

    [Fact]
    public void Group_EveryEntryInACategoryPinned_DropsThatCategorySection() {
        var groups = ToolkitFilter.Group([Pinned("only", ToolkitCategory.Folders)], null, null);

        Assert.Equal(ToolkitGroup.PinnedHeader, Assert.Single(groups).Header);
    }

    /// <summary>The chip and the search box still apply to pinned rows, so narrowing to one category
    /// does not drag unrelated pins onto the page with it.</summary>
    [Fact]
    public void Group_PinnedEntries_AreStillSubjectToTheChipAndTheTerm() {
        ToolkitEntry[] entries = [
            Pinned("folder-pin", ToolkitCategory.Folders),
            Pinned("tool-pin", ToolkitCategory.SystemTools),
        ];

        var byChip = ToolkitFilter.Group(entries, ToolkitCategory.Folders, null);
        Assert.Equal(["folder-pin"], byChip[0].Items.Select(e => e.Command));

        var byTerm = ToolkitFilter.Group(entries, null, "tool");
        Assert.Equal(["tool-pin"], byTerm[0].Items.Select(e => e.Command));
    }

    [Fact]
    public void Group_KeepsEntryOrderWithinASection() {
        var entries = new[] { Entry("first"), Entry("second"), Entry("third") };

        var group = Assert.Single(ToolkitFilter.Group(entries, null, null));

        Assert.Equal(["first", "second", "third"], group.Items.Select(e => e.Command));
    }

    [Fact]
    public void Group_NoEntries_ReturnsNoSections() {
        Assert.Empty(ToolkitFilter.Group([], null, null));
    }
}
