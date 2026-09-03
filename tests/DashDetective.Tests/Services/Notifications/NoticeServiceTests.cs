using DashDetective.Services.Notifications;
using DashDetective.Tests.Fakes;
using System.Collections.Generic;
using Xunit;

namespace DashDetective.Tests.Services.Notifications;

/// <summary>Covers <see cref="NoticeService"/>: a confirmation goes up, takes itself down when its
/// window elapses, and a second one replaces the first rather than queueing behind it — a queue would
/// report actions out of step with the clicks that caused them.</summary>
public class NoticeServiceTests {
    private static (NoticeService Notices, FakeUiTimer Timer, List<string?> Seen) Build() {
        var timer = new FakeUiTimer();
        var notices = new NoticeService(timer);
        var seen = new List<string?>();
        notices.Changed += message => seen.Add(message);
        return (notices, timer, seen);
    }

    [Fact]
    public void Show_RaisesChangedAndStartsTheExpiry() {
        var (notices, timer, seen) = Build();

        notices.Show("Widget positions reset");

        Assert.Equal("Widget positions reset", notices.Current);
        Assert.Equal(["Widget positions reset"], seen);
        Assert.True(timer.IsRunning);
    }

    [Fact]
    public void Show_EmptyMessage_IsIgnored() {
        var (notices, timer, seen) = Build();

        notices.Show("");

        Assert.Null(notices.Current);
        Assert.Empty(seen);
        Assert.False(timer.IsRunning);
    }

    /// <summary>The window elapsing is what makes this a confirmation rather than a banner someone has
    /// to clear.</summary>
    [Fact]
    public void Tick_ClearsTheNoticeAndStopsTheTimer() {
        var (notices, timer, seen) = Build();
        notices.Show("Exported to C:\\reports\\one.txt");

        timer.RaiseTick();

        Assert.Null(notices.Current);
        Assert.Equal(["Exported to C:\\reports\\one.txt", null], seen);
        Assert.False(timer.IsRunning);
    }

    /// <summary>A second action replaces the first and gets its own full window — otherwise it would
    /// inherit however much of the first one's was left.</summary>
    [Fact]
    public void Show_Twice_ReplacesTheMessageAndRestartsTheWindow() {
        var (notices, timer, seen) = Build();
        notices.Show("Widget positions reset");

        notices.Show("Diagnostics copied to clipboard");

        Assert.Equal("Diagnostics copied to clipboard", notices.Current);
        Assert.Equal(["Widget positions reset", "Diagnostics copied to clipboard"], seen);
        Assert.Equal(2, timer.StartCount);
        Assert.Equal(2, timer.StopCount);
        Assert.True(timer.IsRunning);
    }

    [Fact]
    public void Dismiss_ClearsTheNoticeAndStopsTheTimer() {
        var (notices, timer, seen) = Build();
        notices.Show("Keyboard shortcuts restored to defaults");

        notices.Dismiss();

        Assert.Null(notices.Current);
        Assert.Equal(["Keyboard shortcuts restored to defaults", null], seen);
        Assert.False(timer.IsRunning);
    }

    /// <summary>Nothing showing means nothing to say. Esc reaches this whenever no banner is up, and a
    /// Changed for a clear that cleared nothing would blank the shell's text for no reason.</summary>
    [Fact]
    public void Dismiss_NothingShowing_RaisesNothing() {
        var (notices, _, seen) = Build();

        notices.Dismiss();

        Assert.Empty(seen);
    }
}
