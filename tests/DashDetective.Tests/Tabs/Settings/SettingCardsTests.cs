using DashDetective.Tabs.Settings;
using System.Linq;
using Xunit;

namespace DashDetective.Tests.Tabs.Settings;

/// <summary>Covers <see cref="SettingCards"/>: every section the catalog uses names a card. A section
/// with no card is a search jump that lands nowhere once that card is folded, and nothing else on the
/// page would notice.</summary>
public class SettingCardsTests {
    [Fact]
    public void WidgetIdFor_NamesACardForEverySectionTheCatalogUses() {
        foreach (var section in SettingCatalog.Instance.All.Select(entry => entry.Section).Distinct())
            Assert.False(string.IsNullOrEmpty(SettingCards.WidgetIdFor(section)),
                         $"No card for the \"{section}\" section.");
    }

    [Fact]
    public void WidgetIdFor_NamesTheSameCardForEverySettingOnIt() {
        Assert.Equal(SettingCards.WidgetIdFor("Appearance"), SettingCards.WidgetIdFor("Appearance"));
        Assert.NotEqual(SettingCards.WidgetIdFor("Appearance"), SettingCards.WidgetIdFor("Navigation"));
    }

    [Fact]
    public void WidgetIdFor_ASectionThisPageDoesNotDraw_IsEmpty() {
        Assert.Equal("", SettingCards.WidgetIdFor("Nothing"));
    }
}
