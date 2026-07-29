using DashDetective.Tabs.FileExplorer;
using Xunit;

namespace DashDetective.Tests.Tabs.FileExplorer;

/// <summary>Covers <see cref="NavigationHistory"/>: the back/forward trail follows the browser rule —
/// visiting somewhere new discards the forward trail, and stepping either way moves entries between the
/// two stacks — so Back and Forward always retrace the path the user actually took.</summary>
public class NavigationHistoryTests {
    [Fact]
    public void NewHistory_HasNowhereToGo() {
        var history = new NavigationHistory();

        Assert.False(history.CanGoBack);
        Assert.False(history.CanGoForward);
    }

    [Fact]
    public void Record_IgnoresABlankOrigin() {
        // The very first folder opened is navigated to from nothing, which must not start a trail.
        var history = new NavigationHistory();

        history.Record("");

        Assert.False(history.CanGoBack);
    }

    [Fact]
    public void TryGoBack_ReturnsThePreviousFolder() {
        var history = new NavigationHistory();
        history.Record(@"C:\one");

        Assert.True(history.TryGoBack(@"C:\two", out var target));
        Assert.Equal(@"C:\one", target);
        Assert.False(history.CanGoBack);
        Assert.True(history.CanGoForward);
    }

    [Fact]
    public void TryGoBack_ReportsFailureWithNoTrail() {
        var history = new NavigationHistory();

        Assert.False(history.TryGoBack(@"C:\one", out var target));
        Assert.Equal("", target);
    }

    [Fact]
    public void TryGoForward_RetracesAStepUndoneByBack() {
        var history = new NavigationHistory();
        history.Record(@"C:\one");
        history.TryGoBack(@"C:\two", out _);

        Assert.True(history.TryGoForward(@"C:\one", out var target));
        Assert.Equal(@"C:\two", target);
        Assert.True(history.CanGoBack);
        Assert.False(history.CanGoForward);
    }

    [Fact]
    public void Record_DiscardsTheForwardTrailOnANewVisit() {
        var history = new NavigationHistory();
        history.Record(@"C:\one");
        history.TryGoBack(@"C:\two", out _);
        Assert.True(history.CanGoForward);

        history.Record(@"C:\one");

        Assert.False(history.CanGoForward);
        Assert.True(history.CanGoBack);
    }

    [Fact]
    public void TryGoBack_WalksTheWholeTrailInOrder() {
        var history = new NavigationHistory();
        history.Record(@"C:\one");
        history.Record(@"C:\two");
        history.Record(@"C:\three");

        Assert.True(history.TryGoBack(@"C:\four", out var first));
        Assert.Equal(@"C:\three", first);
        Assert.True(history.TryGoBack(first, out var second));
        Assert.Equal(@"C:\two", second);
        Assert.True(history.TryGoBack(second, out var third));
        Assert.Equal(@"C:\one", third);
        Assert.False(history.CanGoBack);
    }

    [Fact]
    public void BackThenForward_ReturnsToWhereItStarted() {
        var history = new NavigationHistory();
        history.Record(@"C:\one");
        history.Record(@"C:\two");

        history.TryGoBack(@"C:\three", out var back);
        history.TryGoForward(back, out var forward);

        Assert.Equal(@"C:\two", back);
        Assert.Equal(@"C:\three", forward);
    }
}
