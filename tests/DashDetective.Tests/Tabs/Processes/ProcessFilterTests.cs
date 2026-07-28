using DashDetective.Tabs.Processes;
using Xunit;

namespace DashDetective.Tests.Tabs.Processes;

/// <summary>Covers <see cref="ProcessFilter"/>: the filter box matches on a name substring or a PID
/// prefix, ignores case and surrounding space, and treats a blank term as "show everything", so
/// clearing the box always restores the full list.</summary>
public class ProcessFilterTests {
    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public void Matches_BlankTermMatchesEverything(string? term) {
        Assert.True(ProcessFilter.Matches("chrome", 1234, term));
    }

    [Fact]
    public void Matches_FindsNameSubstringAnywhere() {
        Assert.True(ProcessFilter.Matches("Code.exe", 900, "ode"));
        Assert.True(ProcessFilter.Matches("Code.exe", 900, "Code.exe"));
    }

    [Fact]
    public void Matches_IgnoresNameCase() {
        Assert.True(ProcessFilter.Matches("Explorer", 42, "explorer"));
        Assert.True(ProcessFilter.Matches("explorer", 42, "EXPLORER"));
    }

    [Fact]
    public void Matches_IgnoresSurroundingWhitespace() {
        Assert.True(ProcessFilter.Matches("chrome", 1234, "  chrome  "));
    }

    [Fact]
    public void Matches_FindsPidByPrefixSoTypingNarrows() {
        Assert.True(ProcessFilter.Matches("chrome", 1234, "1"));
        Assert.True(ProcessFilter.Matches("chrome", 1234, "12"));
        Assert.True(ProcessFilter.Matches("chrome", 1234, "1234"));
    }

    [Fact]
    public void Matches_DoesNotMatchAPidFromItsMiddle() {
        Assert.False(ProcessFilter.Matches("chrome", 1234, "23"));
    }

    [Fact]
    public void Matches_RejectsATermInNeitherNameNorPid() {
        Assert.False(ProcessFilter.Matches("chrome", 1234, "firefox"));
        Assert.False(ProcessFilter.Matches("chrome", 1234, "9"));
    }
}
