using DashDetective.Shell.Navigation;
using DashDetective.Tests.Fakes;
using System.Linq;
using Xunit;

namespace DashDetective.Tests.Shell.Navigation;

/// <summary>Covers re-docking, which has three entry points — the right-click dock menu, the Settings
/// control (both through <c>NavPositionOption.SelectCommand</c>) and the drag gesture (through
/// <c>DockTo</c>). All land on the same state, and the position list's selection follows. Every path now
/// fades before it moves, so the edge only changes when the fade timer ticks — see
/// <see cref="NavigationRelocateTests"/> for the fade itself.</summary>
public class NavigationDockingTests {
    private static (NavigationViewModel Bar, FakeUiTimer Relocate) Bar() {
        var relocate = new FakeUiTimer();
        return (new NavigationViewModel(new FakeUiTimer(), relocate), relocate);
    }

    private static NavPositionOption Option(NavigationViewModel bar, NavOrientation orientation) =>
        bar.Positions.Single(position => position.Value == orientation);

    [Theory]
    [InlineData(NavOrientation.Left)]
    [InlineData(NavOrientation.Right)]
    [InlineData(NavOrientation.Top)]
    [InlineData(NavOrientation.Bottom)]
    public void ChoosingAPosition_DocksTheBarToThatEdge(NavOrientation orientation) {
        var (bar, relocate) = Bar();

        Option(bar, orientation).SelectCommand.Execute(null);
        relocate.RaiseTick();

        Assert.Equal(orientation, bar.Orientation);
    }

    [Fact]
    public void ChoosingAPosition_RaisesPositionPicked_SoTheMenuCanDismiss() {
        var (bar, _) = Bar();
        var raised = 0;
        bar.PositionPicked += () => raised++;

        Option(bar, NavOrientation.Bottom).SelectCommand.Execute(null);

        Assert.Equal(1, raised);
    }

    // Re-picking the edge the bar is already on still dismisses the menu, as a menu should.
    [Fact]
    public void ChoosingTheCurrentPosition_StillRaisesPositionPicked() {
        var (bar, _) = Bar();
        var raised = 0;
        bar.PositionPicked += () => raised++;

        Option(bar, bar.Orientation).SelectCommand.Execute(null);

        Assert.Equal(1, raised);
    }

    [Fact]
    public void Redocking_MarksExactlyOnePositionSelected() {
        var (bar, relocate) = Bar();

        Option(bar, NavOrientation.Right).SelectCommand.Execute(null);
        relocate.RaiseTick();

        var selected = Assert.Single(bar.Positions, position => position.IsSelected);
        Assert.Equal(NavOrientation.Right, selected.Value);
    }

    // The drag gesture goes through DockTo rather than the menu, but must leave the same state behind
    // so the menu and the Settings control both show the new edge.
    [Fact]
    public void DockTo_MatchesTheMenuPath() {
        var (dragged, draggedRelocate) = Bar();
        var (picked, pickedRelocate) = Bar();

        dragged.DockTo(NavOrientation.Top);
        draggedRelocate.RaiseTick();
        Option(picked, NavOrientation.Top).SelectCommand.Execute(null);
        pickedRelocate.RaiseTick();

        Assert.Equal(NavOrientation.Top, dragged.Orientation);
        Assert.Equal(picked.Orientation, dragged.Orientation);
        Assert.Equal(
            picked.Positions.Select(p => p.IsSelected),
            dragged.Positions.Select(p => p.IsSelected));
    }

    [Fact]
    public void NewBar_StartsDockedLeftWithThatPositionSelected() {
        var (bar, _) = Bar();

        Assert.Equal(NavOrientation.Left, bar.Orientation);
        Assert.True(Option(bar, NavOrientation.Left).IsSelected);
    }
}
