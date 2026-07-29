using DashDetective.Shell.Search;
using System.Linq;
using Xunit;

namespace DashDetective.Tests.Shell.Search;

/// <summary>Covers <see cref="RecentSearches"/>: newest first, no duplicates, a bounded list, and a
/// persisted form that survives paths and labels containing any ordinary punctuation while shrugging
/// off a hand-edited settings file.</summary>
public class RecentSearchesTests {
    private static SearchResult Result(
        SearchCategory category, string title, string subtitle = "", string? key = null) =>
        new(category, title, subtitle, 500, () => { }, Key: key);

    private static RecentSearches WithEntries(params string[] titles) {
        var recents = new RecentSearches();
        foreach (var title in titles)
            recents.Remember(Result(SearchCategory.Page, title));
        return recents;
    }

    [Fact]
    public void Remember_PutsTheNewestFirst() {
        var recents = WithEntries("first", "second", "third");

        Assert.Equal(["third", "second", "first"], recents.Entries.Select(e => e.Title));
    }

    [Fact]
    public void Remember_PromotesSomethingOpenedAgainRatherThanListingItTwice() {
        var recents = WithEntries("first", "second", "first");

        Assert.Equal(["first", "second"], recents.Entries.Select(e => e.Title));
    }

    [Fact]
    public void Remember_TellsCategoriesApartWhenTheNameIsShared() {
        // A "Storage" page and a "Storage" folder are different things and both deserve a place.
        var recents = new RecentSearches();
        recents.Remember(Result(SearchCategory.Page, "Storage"));
        recents.Remember(Result(SearchCategory.File, "Storage"));

        Assert.Equal(2, recents.Entries.Count);
    }

    [Fact]
    public void Remember_MatchesOnIdentityRatherThanTitle() {
        // Two files of the same name in different folders are different files.
        var recents = new RecentSearches();
        recents.Remember(Result(SearchCategory.File, "report.txt", key: @"C:\a\report.txt"));
        recents.Remember(Result(SearchCategory.File, "report.txt", key: @"C:\b\report.txt"));

        Assert.Equal(2, recents.Entries.Count);
    }

    [Fact]
    public void Remember_DropsTheOldestOnceTheListIsFull() {
        var recents = new RecentSearches();
        for (var i = 0; i < RecentSearches.MaxEntries + 3; i++)
            recents.Remember(Result(SearchCategory.Page, "page" + i));

        Assert.Equal(RecentSearches.MaxEntries, recents.Entries.Count);
        Assert.DoesNotContain(recents.Entries, e => e.Title == "page0");
    }

    [Fact]
    public void Remember_AnnouncesTheChangeSoItCanBePersisted() {
        var recents = new RecentSearches();
        var changes = 0;
        recents.Changed += () => changes++;

        recents.Remember(Result(SearchCategory.Page, "page"));

        Assert.Equal(1, changes);
    }

    [Fact]
    public void Forget_RemovesAnEntryThatNoLongerNamesAnything() {
        var recents = WithEntries("first", "second");
        var changes = 0;
        recents.Changed += () => changes++;

        recents.Forget(recents.Entries[0]);

        Assert.Equal(["first"], recents.Entries.Select(e => e.Title));
        Assert.Equal(1, changes);
    }

    [Fact]
    public void Forget_StaysQuietAboutAnEntryThatWasNotThere() {
        var recents = WithEntries("first");
        var changes = 0;
        recents.Changed += () => changes++;

        recents.Forget(new RecentSearch(SearchCategory.Page, "gone", "gone", ""));

        Assert.Single(recents.Entries);
        Assert.Equal(0, changes);
    }

    [Fact]
    public void EncodeThenLoad_RoundTripsTheWholeList() {
        var original = new RecentSearches();
        original.Remember(Result(SearchCategory.File, "report.txt", @"C:\Docs", @"C:\Docs\report.txt"));
        original.Remember(Result(SearchCategory.Setting, "Theme", "Appearance", "Theme"));

        var loaded = new RecentSearches();
        loaded.Load(original.Encode());

        Assert.Equal(original.Entries, loaded.Entries);
    }

    [Fact]
    public void EncodeThenLoad_SurvivesPunctuationInPathsAndLabels() {
        // The separators are control characters precisely so no real path or label can contain them.
        var original = new RecentSearches();
        original.Remember(Result(
            SearchCategory.File, "q4 report (v2).txt", @"D:\Work — 2026\o'brien",
            @"D:\Work — 2026\o'brien\q4 report (v2).txt"));

        var loaded = new RecentSearches();
        loaded.Load(original.Encode());

        Assert.Equal(original.Entries[0], loaded.Entries[0]);
    }

    [Fact]
    public void Load_ReplacesWhateverWasThere() {
        var recents = WithEntries("stale");

        recents.Load(WithEntries("fresh").Encode());

        Assert.Equal(["fresh"], recents.Entries.Select(e => e.Title));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not an entry")]
    [InlineData("NotACategory\u001fkey\u001ftitle\u001fsubtitle")]
    [InlineData("Page\u001f\u001ftitle\u001fsubtitle")]
    [InlineData("Page\u001fkey\u001ftitle")]
    public void Load_ShrugsOffAHandEditedFile(string? encoded) {
        var recents = new RecentSearches();

        recents.Load(encoded);

        Assert.Empty(recents.Entries);
    }

    [Fact]
    public void Load_KeepsTheGoodEntriesFromAPartlyBrokenFile() {
        var good = WithEntries("kept").Encode();

        var recents = new RecentSearches();
        recents.Load("rubbish\u001e" + good);

        Assert.Equal(["kept"], recents.Entries.Select(e => e.Title));
    }

    [Fact]
    public void Load_StopsAtTheMaximumHoweverLongTheFile() {
        var recents = new RecentSearches();
        for (var i = 0; i < RecentSearches.MaxEntries + 5; i++)
            recents.Remember(Result(SearchCategory.Page, "page" + i));
        var overlong = recents.Encode() + "\u001eOverflow";

        var loaded = new RecentSearches();
        loaded.Load(overlong);

        Assert.Equal(RecentSearches.MaxEntries, loaded.Entries.Count);
    }

    [Fact]
    public void Encode_IsEmptyForAnEmptyList() {
        Assert.Equal("", new RecentSearches().Encode());
    }
}
