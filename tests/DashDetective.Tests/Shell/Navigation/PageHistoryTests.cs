using DashDetective.Shell.Navigation;
using Xunit;

namespace DashDetective.Tests.Shell.Navigation;

/// <summary>
/// Covers <see cref="PageHistory{T}"/>: each visit pushes the page left behind, a repeat of the current
/// page records nothing, stepping back pops without pushing (so the visit it causes is absorbed and two
/// Backs walk two pages back), the trail is capped at <see cref="PageHistory{T}.Capacity"/> by dropping
/// the oldest, and Back at the start does nothing.
/// </summary>
public class PageHistoryTests {
    /// <summary>A history that has visited each page in turn.</summary>
    private static PageHistory<string> Visited(params string[] pages) {
        var history = new PageHistory<string>();
        foreach (var page in pages)
            history.Visit(page);
        return history;
    }

    [Fact]
    public void Visit_FirstPage_StartsNoTrail() {
        var history = Visited("Dashboard");

        Assert.False(history.CanGoBack);
        Assert.Null(history.Previous);
        Assert.Equal("Dashboard", history.Current);
    }

    [Fact]
    public void Visit_NewPage_PushesThePageLeftBehind() {
        var history = Visited("Dashboard", "Processes");

        Assert.True(history.CanGoBack);
        Assert.Equal("Dashboard", history.Previous);
        Assert.Equal("Processes", history.Current);
    }

    [Fact]
    public void Visit_SamePageAgain_RecordsNothing() {
        var history = Visited("Dashboard", "Processes", "Processes");

        Assert.True(history.TryGoBack(out _));
        Assert.False(history.CanGoBack);
    }

    [Fact]
    public void TryGoBack_TwiceAfterThreeVisits_WalksBackTwoPages() {
        var history = Visited("Dashboard", "Processes", "Network");

        Assert.True(history.TryGoBack(out var first));
        history.Visit(first);
        Assert.True(history.TryGoBack(out var second));
        history.Visit(second);

        Assert.Equal("Processes", first);
        Assert.Equal("Dashboard", second);
        Assert.False(history.CanGoBack);
    }

    /// <summary>The shell navigates to the page Back hands out, which reports a visit; that visit must
    /// not push the page Back just left, or the next Back would bounce straight back to it.</summary>
    [Fact]
    public void TryGoBack_VisitItCauses_IsAbsorbed() {
        var history = Visited("Dashboard", "Processes");

        Assert.True(history.TryGoBack(out var target));
        history.Visit(target);

        Assert.Equal("Dashboard", history.Current);
        Assert.False(history.CanGoBack);
    }

    [Fact]
    public void TryGoBack_AtTheStart_DoesNothing() {
        var history = Visited("Dashboard");

        Assert.False(history.TryGoBack(out var target));
        Assert.Null(target);
        Assert.Equal("Dashboard", history.Current);
    }

    [Fact]
    public void TryGoBack_BeforeAnyVisit_DoesNothing() {
        var history = new PageHistory<string>();

        Assert.False(history.TryGoBack(out _));
        Assert.Null(history.Current);
    }

    [Fact]
    public void Visit_PastCapacity_DropsTheOldest() {
        var history = new PageHistory<string>();
        for (var i = 0; i <= PageHistory<string>.Capacity + 1; i++)
            history.Visit($"page{i}");

        var steps = 0;
        string? last = null;
        while (history.TryGoBack(out var target)) {
            last = target;
            steps++;
        }

        Assert.Equal(PageHistory<string>.Capacity, steps);
        Assert.Equal("page1", last);
    }
}
