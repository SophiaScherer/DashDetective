using DashDetective.Services.Search;
using System.IO;
using Xunit;

namespace DashDetective.Tests.Services.Search;

/// <summary>Covers <see cref="SearchScopes"/>: the user's profile is always searched, the folder the
/// File Explorer is showing is added only when it lies outside that profile, and a duplicate or
/// malformed scope never doubles the results.</summary>
public class SearchScopesTests {
    private const string Profile = @"C:\Users\Sophia";

    [Fact]
    public void For_SearchesTheProfileWhenTheExplorerIsIdle() {
        Assert.Equal([Profile], SearchScopes.For(null, Profile));
    }

    [Theory]
    [InlineData(@"C:\Users\Sophia")]
    [InlineData(@"C:\Users\Sophia\")]
    [InlineData(@"C:\Users\Sophia\Documents")]
    [InlineData(@"c:\users\sophia\documents\projects")]
    public void For_DoesNotReAddAFolderAlreadyInsideTheProfile(string currentFolder) {
        Assert.Equal([Profile], SearchScopes.For(currentFolder, Profile));
    }

    [Fact]
    public void For_AddsAFolderOutsideTheProfile() {
        // Someone browsing a data drive and reaching for search means that drive, not their home folder.
        Assert.Equal([Profile, @"D:\Media"], SearchScopes.For(@"D:\Media", Profile));
    }

    [Fact]
    public void For_IsNotFooledByAProfileLookalike() {
        // "C:\Users\SophiaOld" starts with the profile path as a string but is a different folder.
        Assert.Equal([Profile, @"C:\Users\SophiaOld"], SearchScopes.For(@"C:\Users\SophiaOld", Profile));
    }

    [Fact]
    public void For_ResolvesARelativeWalkBackIntoTheProfile() {
        var walked = Path.Combine(Profile, "Documents", "..", "Downloads");

        Assert.Equal([Profile], SearchScopes.For(walked, Profile));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void For_IgnoresAnEmptyCurrentFolder(string currentFolder) {
        Assert.Equal([Profile], SearchScopes.For(currentFolder, Profile));
    }

    [Fact]
    public void For_StillSearchesTheOpenFolderWithNoProfileToFallBackOn() {
        Assert.Equal([@"D:\Media"], SearchScopes.For(@"D:\Media", ""));
    }

    [Fact]
    public void For_ReturnsNothingToSearchWhenThereIsNowhereToLook() {
        Assert.Empty(SearchScopes.For(null, ""));
    }
}
