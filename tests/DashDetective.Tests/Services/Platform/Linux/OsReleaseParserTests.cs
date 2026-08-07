using DashDetective.Services.Platform.Linux;
using DashDetective.Tests.Fakes;
using System.Collections.Generic;
using Xunit;

namespace DashDetective.Tests.Services.Platform.Linux;

/// <summary>Covers <see cref="OsReleaseParser"/>: the quote stripping that is the whole point of the class
/// — the file is a shell fragment, so the same body mixes quoted and bare values — and the comments and
/// malformed lines a free-form distro file is expected to contain.</summary>
public class OsReleaseParserTests {
    private static IReadOnlyDictionary<string, string> Parse(params string[] lines) =>
        OsReleaseParser.Parse(lines);

    private static IReadOnlyDictionary<string, string> ParseFixture() =>
        OsReleaseParser.Parse(ProcFixtures.OsRelease.Split('\n'));

    /// <summary>The common case, and the one that shows on screen: a double-quoted display name arrives
    /// without its quotes.</summary>
    [Fact]
    public void Parse_StripsDoubleQuotes() =>
        Assert.Equal("Ubuntu 24.04.1 LTS", OsReleaseParser.Value(ParseFixture(), "PRETTY_NAME"));

    /// <summary>Bare values are just as legal, and the same file carries both — a parser that only
    /// handles quoted values leaves the quotes on, and one that blindly trims two characters eats real
    /// ones.</summary>
    [Fact]
    public void Parse_KeepsUnquotedValueIntact() =>
        Assert.Equal("24.04", OsReleaseParser.Value(ParseFixture(), "VERSION_ID"));

    [Fact]
    public void Parse_StripsSingleQuotes() =>
        Assert.Equal("Arch Linux", OsReleaseParser.Value(Parse("PRETTY_NAME='Arch Linux'"), "PRETTY_NAME"));

    /// <summary>An unbalanced quote is left alone rather than half-stripped: it is more likely a value
    /// that genuinely contains one than a quoting the parser should complete.</summary>
    [Fact]
    public void Parse_UnbalancedQuote_IsLeftAlone() =>
        Assert.Equal("\"Ubuntu", OsReleaseParser.Value(Parse("NAME=\"Ubuntu"), "NAME"));

    /// <summary>Only the first <c>=</c> separates key from value, so a URL's query string survives.</summary>
    [Fact]
    public void Parse_SplitsOnTheFirstEqualsOnly() =>
        Assert.Equal("https://www.ubuntu.com/?q=1", OsReleaseParser.Value(ParseFixture(), "HOME_URL"));

    [Fact]
    public void Parse_SkipsCommentsAndBlankLines() {
        var fields = Parse("# a comment", "", "   ", "ID=ubuntu");

        Assert.Equal("ubuntu", Assert.Single(fields).Value);
    }

    /// <summary>A line with no <c>=</c> is skipped rather than failing the parse — the file is free to
    /// carry anything, and one bad line must not blank the panel.</summary>
    [Fact]
    public void Parse_SkipsLinesWithNoAssignment() => Assert.Empty(Parse("this is not an assignment"));

    /// <summary>An absent key reports "", the "not reported" contract every caller treats as missing.</summary>
    [Fact]
    public void Value_AbsentKey_IsEmpty() => Assert.Equal("", OsReleaseParser.Value(Parse(), "PRETTY_NAME"));
}
