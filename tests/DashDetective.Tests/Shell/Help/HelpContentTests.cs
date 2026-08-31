using DashDetective.Shell.Help;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace DashDetective.Tests.Shell.Help;

/// <summary>Covers <see cref="HelpContent"/>: the shipped copy is present, trimmed, punctuated and
/// free of duplicates, so a content edit can't quietly ship blank or repeated entries.</summary>
public class HelpContentTests {
    public static TheoryData<string> Sections => new() { nameof(HelpContent.GettingStarted), nameof(HelpContent.Tips) };

    private static IReadOnlyList<HelpTopic> Section(string name) =>
        name == nameof(HelpContent.GettingStarted) ? HelpContent.GettingStarted : HelpContent.Tips;

    [Fact]
    public void Description_IsPresentAndTrimmed() {
        Assert.False(string.IsNullOrWhiteSpace(HelpContent.Description));
        Assert.Equal(HelpContent.Description, HelpContent.Description.Trim());
    }

    [Fact]
    public void Description_NamesTheApp() {
        Assert.Contains("DashDetective", HelpContent.Description);
    }

    [Theory]
    [MemberData(nameof(Sections))]
    public void Section_IsNotEmpty(string name) {
        Assert.NotEmpty(Section(name));
    }

    [Theory]
    [MemberData(nameof(Sections))]
    public void Section_BodiesAreTrimmedAndSentenceTerminated(string name) {
        foreach (var topic in Section(name)) {
            Assert.False(string.IsNullOrWhiteSpace(topic.Body));
            Assert.Equal(topic.Body.Trim(), topic.Body);
            Assert.EndsWith(".", topic.Body);
        }
    }

    [Theory]
    [MemberData(nameof(Sections))]
    public void Section_BodiesContainNoDuplicates(string name) {
        var section = Section(name);
        Assert.Equal(section.Count, section.Select(topic => topic.Body).Distinct().Count());
    }

    /// <summary>Keys are the search identity and the reveal target, so a repeat would flash the wrong row.</summary>
    [Fact]
    public void Keys_AreUniqueAcrossEverySection() {
        var keys = HelpContent.GettingStarted.Concat(HelpContent.Tips).Select(topic => topic.Key).ToList();
        Assert.Equal(keys.Count, keys.Distinct().Count());
        Assert.All(keys, key => Assert.False(string.IsNullOrWhiteSpace(key)));
    }

    [Fact]
    public void GettingStarted_EntriesAreTitled() {
        foreach (var topic in HelpContent.GettingStarted)
            Assert.False(string.IsNullOrWhiteSpace(topic.Title));
    }

    /// <summary>Tips render as bullets with no heading, so a title on one would go unshown.</summary>
    [Fact]
    public void Tips_AreUntitled() {
        foreach (var tip in HelpContent.Tips)
            Assert.Null(tip.Title);
    }
}
