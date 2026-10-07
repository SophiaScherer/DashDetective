using DashDetective.Services.Identity;
using DashDetective.Shell.Navigation;
using DashDetective.Tests.Fakes;
using Xunit;

namespace DashDetective.Tests.Shell.Navigation;

/// <summary>Covers when the collapse puck is shown. Hover reveals it at once, but leaving the bar only
/// starts a grace period — a bare :pointerover rule took it away the instant the pointer wobbled off the
/// rail, which is what made it hard to click. A drag masks it without disturbing the hover state.</summary>
public class NavigationChevronRevealTests {
    private static (NavigationViewModel Bar, FakeUiTimer Hide) Bar() {
        var hide = new FakeUiTimer();
        return (new NavigationViewModel(hide, new FakeUiTimer(), new UnsupportedUserPictureProvider()), hide);
    }

    [Fact]
    public void Puck_IsHiddenBeforeThePointerArrives() {
        var (bar, _) = Bar();

        Assert.False(bar.ShowChevron);
    }

    [Fact]
    public void PointerEntering_ShowsThePuckImmediately() {
        var (bar, hide) = Bar();

        bar.PointerEnteredBar();

        Assert.True(bar.ShowChevron);
        Assert.False(hide.IsRunning);
    }

    // Leaving arms the timer but changes nothing yet: the puck has to survive the reach towards it.
    [Fact]
    public void PointerLeaving_KeepsThePuckUntilTheGracePeriodElapses() {
        var (bar, hide) = Bar();
        bar.PointerEnteredBar();

        bar.PointerExitedBar();

        Assert.True(bar.ShowChevron);
        Assert.True(hide.IsRunning);
    }

    [Fact]
    public void PuckHides_OnceTheGracePeriodElapses() {
        var (bar, hide) = Bar();
        bar.PointerEnteredBar();
        bar.PointerExitedBar();

        hide.RaiseTick();

        Assert.False(bar.ShowChevron);
        Assert.False(hide.IsRunning);
    }

    // The whole point of the delay: coming back within the window cancels the pending hide.
    [Fact]
    public void ReturningWithinTheGracePeriod_CancelsThePendingHide() {
        var (bar, hide) = Bar();
        bar.PointerEnteredBar();
        bar.PointerExitedBar();

        bar.PointerEnteredBar();
        hide.RaiseTick();

        Assert.True(bar.ShowChevron);
    }

    [Fact]
    public void Dragging_MasksThePuckWithoutClearingTheHover() {
        var (bar, _) = Bar();
        bar.PointerEnteredBar();

        bar.IsDragging = true;

        Assert.False(bar.ShowChevron);
        Assert.True(bar.IsChevronVisible);
    }

    // A gesture that ends with the pointer still on the bar brings the puck straight back.
    [Fact]
    public void DragEnding_RestoresThePuckWhenThePointerIsStillOnTheBar() {
        var (bar, _) = Bar();
        bar.PointerEnteredBar();
        bar.IsDragging = true;

        bar.IsDragging = false;

        Assert.True(bar.ShowChevron);
    }
}
