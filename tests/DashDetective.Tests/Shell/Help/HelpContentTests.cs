using DashDetective.Shell.Help;
using System.Linq;
using Xunit;

namespace DashDetective.Tests.Shell.Help;

/// <summary>Covers <see cref="HelpContent"/>: the shipped copy is present, trimmed, punctuated and
/// free of duplicates, so a content edit can't quietly ship blank or repeated bullets.</summary>
public class HelpContentTests {
    [Fact]
    public void Description_IsPresentAndTrimmed() {
        Assert.False(string.IsNullOrWhiteSpace(HelpContent.Description));
        Assert.Equal(HelpContent.Description, HelpContent.Description.Trim());
    }

    [Fact]
    public void Description_NamesTheApp() {
        Assert.Contains("DashDetective", HelpContent.Description);
    }

    [Fact]
    public void Tips_AreNotEmpty() {
        Assert.NotEmpty(HelpContent.Tips);
    }

    [Fact]
    public void Tips_EachIsTrimmedAndSentenceTerminated() {
        foreach (var tip in HelpContent.Tips) {
            Assert.False(string.IsNullOrWhiteSpace(tip));
            Assert.Equal(tip.Trim(), tip);
            Assert.EndsWith(".", tip);
        }
    }

    [Fact]
    public void Tips_ContainNoDuplicates() {
        Assert.Equal(HelpContent.Tips.Count, HelpContent.Tips.Distinct().Count());
    }
}
