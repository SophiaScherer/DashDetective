using DashDetective.Services.Search;
using Xunit;

namespace DashDetective.Tests.Services.Search;

/// <summary>Covers <see cref="SearchTermEscaper"/>. The Windows Search provider cannot bind parameters
/// inside <c>CONTAINS</c>, so the term is inlined into the SQL — which makes this the only thing
/// stopping a typed character from ending the quoted phrase, ending the string literal, or adding a
/// wildcard of its own.</summary>
public class SearchTermEscaperTests {
    [Fact]
    public void Escape_PassesAnOrdinaryTermThrough() {
        Assert.Equal("report", SearchTermEscaper.Escape("report"));
    }

    [Fact]
    public void Escape_TrimsSurroundingWhitespaceButKeepsInnerSpaces() {
        Assert.Equal("my report", SearchTermEscaper.Escape("  my report  "));
    }

    [Fact]
    public void Escape_DoublesASingleQuoteSoItCannotEndTheLiteral() {
        Assert.Equal("sophia''s notes", SearchTermEscaper.Escape("sophia's notes"));
    }

    [Theory]
    [InlineData("re\"port", "report")]           // would close the quoted phrase
    [InlineData("re*port", "report")]            // the wildcard is ours to add, not the user's
    [InlineData("re?port", "report")]
    [InlineData("re\\port", "report")]           // escape character in the phrase
    public void Escape_StripsTheCharactersThatWouldChangeTheQuerysMeaning(string term, string expected) {
        Assert.Equal(expected, SearchTermEscaper.Escape(term));
    }

    [Fact]
    public void Escape_StripsControlCharacters() {
        Assert.Equal("report", SearchTermEscaper.Escape("re\u0000po\trt"));
    }

    [Fact]
    public void Escape_KeepsPunctuationThatAppearsInRealFilenames() {
        Assert.Equal("q4-report_final (v2).txt", SearchTermEscaper.Escape("q4-report_final (v2).txt"));
    }

    [Fact]
    public void Escape_CapsAnOverlongTerm() {
        var escaped = SearchTermEscaper.Escape(new string('a', 500));

        Assert.NotNull(escaped);
        Assert.Equal(64, escaped.Length);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\"")]
    [InlineData("***")]
    [InlineData("\u0000")]
    public void Escape_ReturnsNullWhenNothingUsableSurvives(string? term) {
        // The caller skips the query entirely rather than sending one that matches everything.
        Assert.Null(SearchTermEscaper.Escape(term));
    }

    [Fact]
    public void EscapeScope_DoublesQuotesInAPathThatLegallyContainsThem() {
        Assert.Equal(@"C:\Users\o''brien\Docs", SearchTermEscaper.EscapeScope(@"C:\Users\o'brien\Docs"));
    }

    [Fact]
    public void EscapeScope_LeavesAnOrdinaryPathAlone() {
        Assert.Equal(@"C:\Users\User", SearchTermEscaper.EscapeScope(@"C:\Users\User"));
    }
}
