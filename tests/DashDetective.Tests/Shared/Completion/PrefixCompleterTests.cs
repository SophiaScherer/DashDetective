using DashDetective.Shared.Completion;
using Xunit;

namespace DashDetective.Tests.Shared.Completion;

/// <summary>Covers <see cref="PrefixCompleter"/>, shared by every field that ghosts a suggestion. The
/// rule that matters: one match completes to the whole thing, several complete only as far as they
/// agree — a ghost that guesses between candidates is wrong half the time.</summary>
public class PrefixCompleterTests {
    private static readonly string[] Folders = ["Documents", "Downloads", "Desktop", "Music"];

    [Fact]
    public void Complete_FillsInTheRestOfASingleMatch() {
        Assert.Equal("Documents", PrefixCompleter.Complete("Doc", Folders));
    }

    [Fact]
    public void Complete_StopsWhereSeveralMatchesStopAgreeing() {
        // Documents and Downloads share only "Do", which is all that is already typed.
        Assert.Null(PrefixCompleter.Complete("Do", Folders));

        // Documents, Downloads and Desktop share only "D".
        Assert.Null(PrefixCompleter.Complete("D", Folders));
    }

    [Fact]
    public void Complete_SuggestsTheAgreedPartWhenItAddsSomething() {
        // Both add "own" before diverging, so that much is safe to ghost.
        Assert.Equal("Down", PrefixCompleter.Complete("D", ["Download", "Downstairs"]));
    }

    [Fact]
    public void Complete_IgnoresCaseWhenMatching() {
        Assert.Equal("Documents", PrefixCompleter.Complete("doc", Folders));
        Assert.Equal("Documents", PrefixCompleter.Complete("DOC", Folders));
    }

    [Fact]
    public void Complete_SuggestsNothingForAFullyTypedWord() {
        Assert.Null(PrefixCompleter.Complete("Music", Folders));
        Assert.Null(PrefixCompleter.Complete("music", Folders));
    }

    [Fact]
    public void Complete_SuggestsNothingWhenNothingMatches() {
        Assert.Null(PrefixCompleter.Complete("zzz", Folders));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Complete_SuggestsNothingBeforeAnythingIsTyped(string? typed) {
        // Otherwise an empty box would ghost whichever candidate happened to come first.
        Assert.Null(PrefixCompleter.Complete(typed, Folders));
    }

    [Fact]
    public void Complete_SuggestsNothingWithNoCandidates() {
        Assert.Null(PrefixCompleter.Complete("Doc", []));
    }

    [Fact]
    public void Complete_SkipsEmptyCandidates() {
        Assert.Equal("Documents", PrefixCompleter.Complete("Doc", ["", "Documents"]));
    }

    [Fact]
    public void Suffix_ReturnsOnlyThePartPastWhatWasTyped() {
        Assert.Equal("uments", PrefixCompleter.Suffix("Doc", "Documents"));
    }

    [Fact]
    public void Suffix_KeepsWorkingAsTheUserTypesIntoTheSuggestion() {
        // The ghost must not blink out between queries while what is typed still leads to it.
        Assert.Equal("ments", PrefixCompleter.Suffix("Docu", "Documents"));
        Assert.Equal("s", PrefixCompleter.Suffix("Document", "Documents"));
    }

    [Fact]
    public void Suffix_IgnoresCaseSoTheUsersOwnTypingIsPreserved() {
        Assert.Equal("uments", PrefixCompleter.Suffix("doc", "Documents"));
    }

    [Theory]
    [InlineData("Documents", "Documents")]  // nothing left to add
    [InlineData("Doc", "Downloads")]        // the completion doesn't extend what was typed
    [InlineData("Doc", null)]
    [InlineData(null, "Documents")]
    public void Suffix_ReturnsNothingToDrawWhenThereIsNoValidGhost(string? typed, string? completion) {
        Assert.Equal("", PrefixCompleter.Suffix(typed, completion));
    }
}
