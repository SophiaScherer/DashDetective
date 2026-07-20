using DashDetective.Services.Identity;
using Xunit;

namespace DashDetective.Tests.Services.Identity;

/// <summary>Covers <see cref="CurrentUserProvider.DeriveInitials"/>: two-token, single-token,
/// one-char and empty inputs (and the split delimiters), all upper-cased.</summary>
public class CurrentUserProviderTests {
    [Theory]
    [InlineData("sophia.schmidt", "SS")]
    [InlineData("a b", "AB")]
    [InlineData("john-paul", "JP")]
    [InlineData("sophia_schmidt-jones", "SS")]   // 3+ tokens → first two tokens' initials
    public void DeriveInitials_TwoOrMoreTokens_UsesFirstLetterOfFirstTwo(string name, string expected) {
        Assert.Equal(expected, CurrentUserProvider.DeriveInitials(name));
    }

    [Fact]
    public void DeriveInitials_SingleToken_UsesFirstTwoLetters() {
        Assert.Equal("SO", CurrentUserProvider.DeriveInitials("sophiasch"));
    }

    [Fact]
    public void DeriveInitials_OneCharacter_UsesThatLetter() {
        Assert.Equal("X", CurrentUserProvider.DeriveInitials("x"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void DeriveInitials_Empty_ReturnsQuestionMark(string name) {
        Assert.Equal("?", CurrentUserProvider.DeriveInitials(name));
    }
}
