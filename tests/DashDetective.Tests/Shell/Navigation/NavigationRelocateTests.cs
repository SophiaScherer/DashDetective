using DashDetective.Services.Identity;
using DashDetective.Shell.Navigation;
using DashDetective.Tests.Fakes;
using Xunit;

namespace DashDetective.Tests.Shell.Navigation;

/// <summary>Covers the re-dock fade. A DockPanel offers no path between edges, so a move is expressed as
/// fade out, change edge while invisible, fade back in. The edge must therefore not change until the
/// fade-out has finished, and every re-dock path has to go through the same deferral.</summary>
public class NavigationRelocateTests {
    private static (NavigationViewModel Bar, FakeUiTimer Relocate) Bar() {
        var relocate = new FakeUiTimer();
        return (new NavigationViewModel(new FakeUiTimer(), relocate, new UnsupportedUserPictureProvider()), relocate);
    }

    [Fact]
    public void NewBar_IsNotRelocating() {
        var (bar, relocate) = Bar();

        Assert.False(bar.IsRelocating);
        Assert.False(relocate.IsRunning);
    }

    [Fact]
    public void Redocking_FadesOutBeforeTheEdgeChanges() {
        var (bar, relocate) = Bar();

        bar.DockTo(NavOrientation.Top);

        Assert.True(bar.IsRelocating);
        Assert.True(relocate.IsRunning);
        Assert.Equal(NavOrientation.Left, bar.Orientation);
    }

    // The move takes two beats. The first changes the edge while the bar is still faded out and the size
    // transitions still suspended; only the second fades it back in, once the layout has settled.
    [Fact]
    public void TheFirstTick_ChangesTheEdgeButStaysFadedOut() {
        var (bar, relocate) = Bar();
        bar.DockTo(NavOrientation.Top);

        relocate.RaiseTick();

        Assert.Equal(NavOrientation.Top, bar.Orientation);
        Assert.True(bar.IsRelocating);
        Assert.True(relocate.IsRunning);
    }

    [Fact]
    public void TheSecondTick_FadesTheBarBackIn() {
        var (bar, relocate) = Bar();
        bar.DockTo(NavOrientation.Top);

        relocate.RaiseTick();
        relocate.RaiseTick();

        Assert.Equal(NavOrientation.Top, bar.Orientation);
        Assert.False(bar.IsRelocating);
        Assert.False(relocate.IsRunning);
    }

    // Re-picking the edge the bar already sits on is not a move, so it must not blink.
    [Fact]
    public void RedockingToTheCurrentEdge_DoesNothing() {
        var (bar, relocate) = Bar();

        bar.DockTo(NavOrientation.Left);

        Assert.False(bar.IsRelocating);
        Assert.False(relocate.IsRunning);
    }

    [Fact]
    public void ASecondPickBeforeTheTick_LandsOnTheLastEdgeAskedFor() {
        var (bar, relocate) = Bar();

        bar.DockTo(NavOrientation.Top);
        bar.DockTo(NavOrientation.Bottom);
        relocate.RaiseTick();

        Assert.Equal(NavOrientation.Bottom, bar.Orientation);
        Assert.True(bar.IsRelocating);
    }

    // Mid-fade the pending target, not the current edge, is what a new pick replaces — so asking for the
    // edge being left is a real change of mind, not a no-op.
    [Fact]
    public void PickingTheEdgeBeingLeft_MidFade_StaysPut() {
        var (bar, relocate) = Bar();
        bar.DockTo(NavOrientation.Top);

        bar.DockTo(NavOrientation.Left);
        relocate.RaiseTick();
        relocate.RaiseTick();

        Assert.Equal(NavOrientation.Left, bar.Orientation);
        Assert.False(bar.IsRelocating);
    }

    [Fact]
    public void TheSettingsCommand_FadesLikeEveryOtherPath() {
        var (bar, relocate) = Bar();

        bar.SetOrientationCommand.Execute(NavOrientation.Right);

        Assert.True(bar.IsRelocating);
        Assert.Equal(NavOrientation.Left, bar.Orientation);

        relocate.RaiseTick();

        Assert.Equal(NavOrientation.Right, bar.Orientation);
    }
}
