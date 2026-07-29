using DashDetective.Tabs.Settings;
using System;
using System.Linq;
using Xunit;

namespace DashDetective.Tests.Tabs.Settings;

/// <summary>Covers <see cref="SettingCatalog"/>: the table stays in step with <see cref="SettingId"/>
/// (a setting added to the enum but not described here would be unsearchable, and one described twice
/// would appear twice in the results) and every entry carries the copy the page binds to.</summary>
public class SettingCatalogTests {
    private static SettingCatalog Catalog => SettingCatalog.Instance;

    [Fact]
    public void All_DescribesEverySettingIdExactlyOnce() {
        var ids = Catalog.All.Select(e => e.Id).ToList();

        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.Equal(Enum.GetValues<SettingId>().OrderBy(id => id), ids.OrderBy(id => id));
    }

    [Fact]
    public void All_CarriesTrimmedCopyForEveryEntry() {
        foreach (var entry in Catalog.All) {
            Assert.False(string.IsNullOrWhiteSpace(entry.Section));
            Assert.False(string.IsNullOrWhiteSpace(entry.Name));
            Assert.False(string.IsNullOrWhiteSpace(entry.Description));
            Assert.Equal(entry.Section.Trim(), entry.Section);
            Assert.Equal(entry.Name.Trim(), entry.Name);
            Assert.Equal(entry.Description.Trim(), entry.Description);
        }
    }

    [Fact]
    public void All_GroupsEachSectionTogetherInPageOrder() {
        // The page draws one panel per section, so the entries must not interleave.
        var sections = Catalog.All.Select(e => e.Section).ToList();
        var runs = sections.Where((s, i) => i == 0 || s != sections[i - 1]).ToList();

        Assert.Equal(runs.Count, runs.Distinct().Count());
    }

    [Fact]
    public void Get_ReturnsTheEntryForEveryId() {
        foreach (var id in Enum.GetValues<SettingId>())
            Assert.Equal(id, Catalog.Get(id).Id);
    }

    [Fact]
    public void Get_ThrowsForAnIdTheTableDoesNotDescribe() {
        Assert.Throws<ArgumentOutOfRangeException>(() => Catalog.Get((SettingId)999));
    }

    [Fact]
    public void Instance_IsShared() {
        Assert.Same(SettingCatalog.Instance, Catalog);
    }
}
