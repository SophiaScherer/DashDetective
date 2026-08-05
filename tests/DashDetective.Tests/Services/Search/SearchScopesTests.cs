using DashDetective.Services.Search;
using DashDetective.Tests.Fakes;
using System;
using System.IO;
using Xunit;

namespace DashDetective.Tests.Services.Search;

/// <summary>Covers <see cref="SearchScopes"/>: the user's profile is always searched, the folder the
/// File Explorer is showing is added only when it lies outside that profile, and a duplicate or
/// malformed scope never doubles the results.</summary>
public class SearchScopesTests {
    private static readonly string Profile = TestPaths.Of("Users", "Sophia");

    // Somewhere the profile does not contain — a data drive on Windows, another root folder on Linux.
    private static readonly string Outside = TestPaths.Of("Media");

    [Fact]
    public void For_SearchesTheProfileWhenTheExplorerIsIdle() {
        Assert.Equal([Profile], SearchScopes.For(null, Profile));
    }

    [Fact]
    public void For_DoesNotReAddAFolderAlreadyInsideTheProfile() {
        Assert.Equal([Profile], SearchScopes.For(Profile, Profile));
        Assert.Equal([Profile], SearchScopes.For(Profile + Path.DirectorySeparatorChar, Profile));
        Assert.Equal([Profile], SearchScopes.For(TestPaths.Of("Users", "Sophia", "Documents"), Profile));
    }

    /// <summary>Windows folds path case, so a differently-cased spelling of the profile is the profile.
    /// See <c>PathComparison</c>.</summary>
    [Fact]
    public void For_FoldsPathCaseOnWindows() {
        if (!OperatingSystem.IsWindows())
            return;

        Assert.Equal([Profile], SearchScopes.For(@"c:\users\sophia\documents\projects", Profile));
    }

    /// <summary>Off Windows the filesystem is case-sensitive, so the same spelling in another case is a
    /// genuinely different folder and has to be searched as well.</summary>
    [Fact]
    public void For_TreatsACaseVariantAsAnotherFolderOffWindows() {
        if (OperatingSystem.IsWindows())
            return;

        var variant = TestPaths.Of("users", "sophia", "Documents");

        Assert.Equal([Profile, variant], SearchScopes.For(variant, Profile));
    }

    [Fact]
    public void For_AddsAFolderOutsideTheProfile() {
        // Someone browsing a data drive and reaching for search means that drive, not their home folder.
        Assert.Equal([Profile, Outside], SearchScopes.For(Outside, Profile));
    }

    [Fact]
    public void For_IsNotFooledByAProfileLookalike() {
        // "…/Users/SophiaOld" starts with the profile path as a string but is a different folder.
        var lookalike = TestPaths.Of("Users", "SophiaOld");

        Assert.Equal([Profile, lookalike], SearchScopes.For(lookalike, Profile));
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
        Assert.Equal([Outside], SearchScopes.For(Outside, ""));
    }

    [Fact]
    public void For_ReturnsNothingToSearchWhenThereIsNowhereToLook() {
        Assert.Empty(SearchScopes.For(null, ""));
    }
}
