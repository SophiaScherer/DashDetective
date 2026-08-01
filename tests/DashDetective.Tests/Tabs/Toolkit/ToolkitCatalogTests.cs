using DashDetective.Tabs.Toolkit;
using System;
using System.Linq;
using Xunit;

namespace DashDetective.Tests.Tabs.Toolkit;

/// <summary>Covers <see cref="ToolkitCatalog"/>: the copy table stays complete (every category and
/// every kind reads as something, and no two share a label) and the display order matches the enum's
/// declaration order, so adding a category can't silently fall through to another one's text.</summary>
public class ToolkitCatalogTests {
    [Fact]
    public void Categories_ListEveryValueInDeclarationOrder() {
        Assert.Equal(Enum.GetValues<ToolkitCategory>(), ToolkitCatalog.Categories);
    }

    [Fact]
    public void HeaderFor_NamesEveryCategoryDistinctly() {
        var headers = Enum.GetValues<ToolkitCategory>().Select(ToolkitCatalog.HeaderFor).ToList();

        Assert.All(headers, h => Assert.False(string.IsNullOrWhiteSpace(h)));
        Assert.Equal(headers.Count, headers.Distinct().Count());
    }

    [Fact]
    public void LabelFor_NamesEveryKindDistinctly() {
        var labels = Enum.GetValues<ToolkitEntryKind>().Select(ToolkitCatalog.LabelFor).ToList();

        Assert.All(labels, l => Assert.False(string.IsNullOrWhiteSpace(l)));
        Assert.Equal(labels.Count, labels.Distinct().Count());
    }

    /// <summary>Every row must say what it is and what it does — a blank description leaves the row
    /// half-drawn and, because the search provider matches on it, unreachable by intent.</summary>
    [Fact]
    public void Entries_AllCarryACommandAndADescription() {
        Assert.NotEmpty(ToolkitCatalog.Entries);
        Assert.All(ToolkitCatalog.Entries, entry => {
            Assert.False(string.IsNullOrWhiteSpace(entry.Command));
            Assert.False(string.IsNullOrWhiteSpace(entry.Description));
        });
    }

    /// <summary>The command text is a row's identity: universal search reveals by it, and
    /// <c>ToolkitView.FindRow</c> matches the first row whose Tag equals it. A duplicate would make one
    /// of the pair permanently unreachable.</summary>
    [Fact]
    public void Entries_HaveDistinctCommands() {
        var commands = ToolkitCatalog.Entries.Select(entry => entry.Command).ToList();

        Assert.Equal(commands.Count, commands.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void Entries_AllCarryAnActionWithATarget() {
        Assert.All(ToolkitCatalog.Entries,
                   entry => Assert.False(string.IsNullOrWhiteSpace(entry.Action.Target)));
    }

    /// <summary>Every category the entries claim must be one the list actually renders, or the rows
    /// filed under it would be dropped by <see cref="ToolkitFilter.Group"/> and never appear.</summary>
    [Fact]
    public void Entries_OnlyUseCategoriesTheListRenders() {
        Assert.All(ToolkitCatalog.Entries,
                   entry => Assert.Contains(entry.Category, ToolkitCatalog.Categories));
    }

    /// <summary>Elevation is opt-in per entry, so an accidental one is worth catching: only commands
    /// explicitly expected to need admin may ask for it.</summary>
    [Fact]
    public void Entries_DoNotRequireElevationUnlessTheyOpenAFolder() {
        Assert.All(ToolkitCatalog.Entries.Where(e => e.Kind == ToolkitEntryKind.Folder),
                   entry => Assert.False(entry.Action.RequiresElevation));
    }
}
