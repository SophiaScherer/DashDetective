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

    /// <summary>The tab ships as UI only — the command set is authored later, and this is the seam
    /// that changes when it is.</summary>
    [Fact]
    public void Entries_AreEmptyUntilTheCommandSetIsAuthored() {
        Assert.Empty(ToolkitCatalog.Entries);
    }
}
