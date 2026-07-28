using Avalonia.Controls;
using Avalonia.Layout;
using DashDetective.Shell.Navigation;
using System.Collections.Generic;
using Xunit;

namespace DashDetective.Tests.Shell.Navigation;

/// <summary>Covers the computed layout properties that place the footer's cluster: <c>ControlsDock</c>
/// (the Help button) and <c>FooterAvatarDock</c>. Both must stack into a column only on a collapsed
/// vertical rail, which is too narrow to lay them out side by side; every other state runs across.</summary>
public class NavigationViewModelTests {
    private static NavigationViewModel Bar(NavOrientation orientation, bool collapsed) =>
        new() { Orientation = orientation, IsCollapsed = collapsed };

    [Theory]
    [InlineData(NavOrientation.Left, true)]
    [InlineData(NavOrientation.Right, true)]
    public void ControlsDock_CollapsedVerticalRail_StacksBeneath(NavOrientation orientation, bool collapsed) {
        Assert.Equal(Dock.Bottom, Bar(orientation, collapsed).ControlsDock);
    }

    [Theory]
    [InlineData(NavOrientation.Left, false)]
    [InlineData(NavOrientation.Right, false)]
    [InlineData(NavOrientation.Top, false)]
    [InlineData(NavOrientation.Bottom, false)]
    [InlineData(NavOrientation.Top, true)]
    [InlineData(NavOrientation.Bottom, true)]
    public void ControlsDock_EveryOtherState_SitsAtTheTrailingEdge(NavOrientation orientation, bool collapsed) {
        Assert.Equal(Dock.Right, Bar(orientation, collapsed).ControlsDock);
    }

    [Theory]
    [InlineData(NavOrientation.Left, true)]
    [InlineData(NavOrientation.Right, true)]
    public void FooterAvatarDock_CollapsedVerticalRail_LeadsTheColumn(NavOrientation orientation, bool collapsed) {
        Assert.Equal(Dock.Top, Bar(orientation, collapsed).FooterAvatarDock);
    }

    [Theory]
    [InlineData(NavOrientation.Left, false)]
    [InlineData(NavOrientation.Right, false)]
    [InlineData(NavOrientation.Top, false)]
    [InlineData(NavOrientation.Bottom, false)]
    [InlineData(NavOrientation.Top, true)]
    [InlineData(NavOrientation.Bottom, true)]
    public void FooterAvatarDock_EveryOtherState_LeadsTheRow(NavOrientation orientation, bool collapsed) {
        Assert.Equal(Dock.Left, Bar(orientation, collapsed).FooterAvatarDock);
    }

    // The two always bracket the same axis, so the name between them is never squeezed from one side.
    [Theory]
    [InlineData(NavOrientation.Left, false)]
    [InlineData(NavOrientation.Left, true)]
    [InlineData(NavOrientation.Top, true)]
    public void AvatarAndControls_AlwaysBracketTheSameAxis(NavOrientation orientation, bool collapsed) {
        var bar = Bar(orientation, collapsed);
        var vertical = bar.FooterAvatarDock == Dock.Top;
        Assert.Equal(vertical ? Dock.Bottom : Dock.Right, bar.ControlsDock);
    }

    [Fact]
    public void Collapsing_RaisesChangeNotificationsForTheDockedClusters() {
        var bar = Bar(NavOrientation.Left, collapsed: false);
        var changed = new List<string?>();
        bar.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        bar.IsCollapsed = true;

        Assert.Contains(nameof(NavigationViewModel.ControlsDock), changed);
        Assert.Contains(nameof(NavigationViewModel.FooterAvatarDock), changed);
    }

    [Fact]
    public void Redocking_RaisesChangeNotificationsForTheDockedClusters() {
        var bar = Bar(NavOrientation.Left, collapsed: true);
        var changed = new List<string?>();
        bar.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        bar.Orientation = NavOrientation.Top;

        Assert.Contains(nameof(NavigationViewModel.ControlsDock), changed);
        Assert.Contains(nameof(NavigationViewModel.FooterAvatarDock), changed);
    }

    [Fact]
    public void ShowHelp_RaisesHelpRequested() {
        var bar = new NavigationViewModel();
        var raised = 0;
        bar.HelpRequested += () => raised++;

        bar.ShowHelpCommand.Execute(null);

        Assert.Equal(1, raised);
    }
}
