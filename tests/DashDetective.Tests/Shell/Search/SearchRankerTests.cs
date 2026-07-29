using DashDetective.Shell.Search;
using Xunit;

namespace DashDetective.Tests.Shell.Search;

/// <summary>Covers <see cref="SearchRanker"/>: the four match tiers stay strictly ordered (a closeness
/// bonus or a field penalty must never lift a weaker match past a stronger one) and matching is
/// case-insensitive, so results from unrelated providers merge into one honest ordering.</summary>
public class SearchRankerTests {
    [Fact]
    public void Score_OrdersTheFourTiersStrongestFirst() {
        var exact = SearchRanker.Score("network", "network");
        var prefix = SearchRanker.Score("network", "network adapters");
        var wordStart = SearchRanker.Score("network", "show network adapters");
        var anywhere = SearchRanker.Score("network", "internetworking");

        Assert.True(exact > prefix);
        Assert.True(prefix > wordStart);
        Assert.True(wordStart > anywhere);
        Assert.True(anywhere > SearchRanker.NoMatch);
    }

    [Fact]
    public void Score_KeepsTheTiersApartHoweverLongTheText() {
        // The closeness bonus separates results inside a tier; it must not cross one. A near-perfect
        // word-start match still loses to the weakest possible prefix match.
        var shortWordStart = SearchRanker.Score("usage", "a usage");
        var longPrefix = SearchRanker.Score("usage", "usage" + new string('x', 500));

        Assert.True(longPrefix > shortWordStart);
    }

    [Fact]
    public void Score_PrefersTheTighterMatchWithinATier() {
        var tight = SearchRanker.Score("cpu", "cpu core");
        var loose = SearchRanker.Score("cpu", "cpu usage history log");

        Assert.True(tight > loose);
    }

    [Theory]
    [InlineData("NETWORK", "network adapters")]
    [InlineData("network", "NETWORK ADAPTERS")]
    [InlineData("NeTwOrK", "nEtWoRk AdApTeRs")]
    public void Score_IgnoresCase(string term, string text) {
        Assert.True(SearchRanker.Score(term, text) > SearchRanker.NoMatch);
    }

    [Fact]
    public void Score_TreatsSeparatorsAsWordBreaks() {
        // A term after a space, dash, dot or separator begins a word; one mid-word does not.
        Assert.True(SearchRanker.Score("usage", "high-usage") > SearchRanker.Score("usage", "misusage"));
        Assert.True(SearchRanker.Score("report", "export\\report.txt") > SearchRanker.Score("report", "xreportx"));
    }

    [Theory]
    [InlineData("zzz", "network adapters")]
    [InlineData("", "network")]
    [InlineData("   ", "network")]
    [InlineData("network adapters and more", "network")]
    public void Score_ReturnsNoMatchWhenTheTermCannotAppear(string term, string text) {
        Assert.Equal(SearchRanker.NoMatch, SearchRanker.Score(term, text));
    }

    [Fact]
    public void Score_ReturnsNoMatchForMissingText() {
        Assert.Equal(SearchRanker.NoMatch, SearchRanker.Score("network", null));
        Assert.Equal(SearchRanker.NoMatch, SearchRanker.Score("network", ""));
    }

    [Fact]
    public void ScoreBest_KeepsTheStrongestFieldAndDemotesTheLaterOnes() {
        // Same text, once as the title and once as the description: the title match must win.
        var asTitle = SearchRanker.ScoreBest("theme", "theme", "pick a colour scheme");
        var asDescription = SearchRanker.ScoreBest("theme", "appearance", "theme");

        Assert.True(asTitle > asDescription);
    }

    [Fact]
    public void ScoreBest_DemotionNeverDropsAFieldOutOfItsTier() {
        // A prefix match in the last field still beats a word-start match in the first.
        var latePrefix = SearchRanker.ScoreBest("live", "zzz", "zzz", "zzz", "live sampling");
        var earlyWordStart = SearchRanker.ScoreBest("live", "pause live sampling");

        Assert.True(latePrefix > earlyWordStart);
    }

    [Fact]
    public void ScoreBest_ReturnsNoMatchWhenNoFieldMatches() {
        Assert.Equal(SearchRanker.NoMatch, SearchRanker.ScoreBest("zzz", "theme", "appearance", null));
    }
}
