using DashDetective.Services.Search;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DashDetective.Tests.Services.Search;

/// <summary>
/// Covers the query <see cref="WindowsSearchIndex"/> builds. Running it is not tested: whether the index
/// answers is machine state (indexing can be off, still building, or excluded for a folder), and the
/// class soft-fails to "unavailable" by construction for exactly that reason. What is worth pinning down
/// is the SQL — it is assembled by hand around a user-typed term.
/// </summary>
public class WindowsSearchIndexTests {
    private static readonly IReadOnlyList<string> OneScope = [@"C:\Users\Sophia"];

    [Fact]
    public void BuildQuery_AsksTheIndexForNoMoreRowsThanWanted() {
        Assert.Contains("SELECT TOP 20 ", WindowsSearchIndex.BuildQuery("report", OneScope, 20));
    }

    [Fact]
    public void BuildQuery_MatchesOnTheFilenameWithATrailingWildcard() {
        // CONTAINS over System.FileName is the index-accelerated path; the wildcard is what makes a
        // half-typed name match.
        Assert.Contains("CONTAINS(System.FileName, '\"report*\"')",
            WindowsSearchIndex.BuildQuery("report", OneScope, 20));
    }

    [Fact]
    public void BuildQuery_ScopesToTheFolderGiven() {
        Assert.Contains(@"SCOPE='file:C:\Users\Sophia'", WindowsSearchIndex.BuildQuery("report", OneScope, 20));
    }

    [Fact]
    public void BuildQuery_JoinsSeveralScopesAsAlternatives() {
        var sql = WindowsSearchIndex.BuildQuery("report", [@"C:\Users\Sophia", @"D:\Media"], 20);

        Assert.Contains(@"(SCOPE='file:C:\Users\Sophia' OR SCOPE='file:D:\Media')", sql);
    }

    [Fact]
    public void BuildQuery_EscapesAQuoteInAScopePath() {
        var sql = WindowsSearchIndex.BuildQuery("report", [@"C:\Users\o'brien"], 20);

        Assert.Contains(@"SCOPE='file:C:\Users\o''brien'", sql);
    }

    [Fact]
    public void BuildQuery_OrdersByRecencySoActiveWorkSurfacesFirst() {
        Assert.EndsWith("ORDER BY System.DateModified DESC",
            WindowsSearchIndex.BuildQuery("report", OneScope, 20));
    }

    [Fact]
    public void BuildQuery_SelectsEveryColumnAResultRowNeeds() {
        var sql = WindowsSearchIndex.BuildQuery("report", OneScope, 20);

        Assert.Contains("System.ItemNameDisplay", sql);
        Assert.Contains("System.ItemPathDisplay", sql);
        Assert.Contains("System.ItemFolderPathDisplay", sql);
        Assert.Contains("System.ItemType", sql);
        Assert.Contains("System.DateModified", sql);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\"")]
    public async Task SearchAsync_ReportsUnavailableForATermWithNothingToSearchFor(string term) {
        // Rather than sending a query whose escaped term is empty, which would match everything.
        Assert.Null(await new WindowsSearchIndex().SearchAsync(term, OneScope, 20, CancellationToken.None));
    }

    [Fact]
    public async Task SearchAsync_ReportsUnavailableWithNoScopeToSearch() {
        Assert.Null(await new WindowsSearchIndex().SearchAsync("report", [], 20, CancellationToken.None));
    }
}
