using Avalonia.Controls;
using DashDetective.Shared;
using DashDetective.Shared.Shortcuts;
using DashDetective.Shell.Navigation;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace DashDetective.Tests.Shell.Navigation;

/// <summary>Covers the nav bar's section list on the view model: pressing the current page's item again
/// opens it (only when the page has sections), pressing another item navigates and drops the old page's
/// list, opening asks the shell to re-read the page, a pick closes the list and names the section, and
/// Esc closes it without swallowing the key otherwise. <see cref="NavigationViewModel.Navigate"/> must
/// never open it, since shortcuts and search call it too.</summary>
public class NavigationSectionsTests {
    private sealed class Page : ViewModelBase { }

    private static (NavigationViewModel Bar, NavItem First, NavItem Second) Bar() {
        var bar = new NavigationViewModel();
        // Geometry needs a render backend, so the items carry none.
        var first = new NavItem("One", "One", "", null!, new Page(), bar.Activate);
        var second = new NavItem("Two", "Two", "", null!, new Page(), bar.Activate);
        bar.Initialize([first, second]);
        return (bar, first, second);
    }

    private static SectionCandidate[] Two() => [new("Alpha", 0, 0, true), new("Beta", 200, 0, true)];

    [Fact]
    public void SetSections_TwoOrMore_OffersTheList() {
        var (bar, _, _) = Bar();

        bar.SetSections(Two());

        Assert.True(bar.HasSections);
        Assert.Equal(["Alpha", "Beta"], bar.Sections.Select(s => s.Title));
    }

    [Fact]
    public void SetSections_OnlyOne_HidesTheList() {
        var (bar, _, _) = Bar();

        bar.SetSections([new("Alone", 0, 0, true)]);

        Assert.False(bar.HasSections);
    }

    [Fact]
    public void SetSections_RaisesHasSections() {
        var (bar, _, _) = Bar();
        var changed = new List<string?>();
        bar.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        bar.SetSections(Two());

        Assert.Contains(nameof(NavigationViewModel.HasSections), changed);
    }

    [Fact]
    public void Activate_CurrentItemWithSections_TogglesTheList() {
        var (bar, first, _) = Bar();
        bar.SetSections(Two());

        bar.Activate(first);
        Assert.True(bar.IsSectionsOpen);

        bar.Activate(first);
        Assert.False(bar.IsSectionsOpen);
    }

    [Fact]
    public void Activate_CurrentItemWithoutSections_DoesNothing() {
        var (bar, first, _) = Bar();

        bar.Activate(first);

        Assert.False(bar.IsSectionsOpen);
        Assert.Same(first, bar.SelectedNav);
    }

    /// <summary>A page can gain sections without a page change (a form opening), so the press re-reads it
    /// rather than trusting the count from the last page change.</summary>
    [Fact]
    public void Activate_PageGainedSectionsSinceLastRead_RereadsAndOpens() {
        var (bar, first, _) = Bar();
        bar.SectionsRefreshRequested += () => bar.SetSections(Two());

        bar.Activate(first);

        Assert.True(bar.IsSectionsOpen);
    }

    [Fact]
    public void Activate_AnotherItem_NavigatesAndDropsTheOldPagesList() {
        var (bar, _, second) = Bar();
        bar.SetSections(Two());
        bar.IsSectionsOpen = true;

        bar.Activate(second);

        Assert.Same(second, bar.SelectedNav);
        Assert.False(bar.IsSectionsOpen);
        Assert.False(bar.HasSections);
    }

    // Ctrl+1, search and cross-page jumps all land here; landing on the current page must not open it.
    [Fact]
    public void Navigate_CurrentItem_NeverOpensTheList() {
        var (bar, first, _) = Bar();
        bar.SetSections(Two());

        bar.Navigate(first);

        Assert.False(bar.IsSectionsOpen);
    }

    [Fact]
    public void Opening_AsksTheShellToReReadThePage() {
        var (bar, _, _) = Bar();
        bar.SetSections(Two());
        var asked = 0;
        bar.SectionsRefreshRequested += () => asked++;

        bar.IsSectionsOpen = true;

        Assert.Equal(1, asked);
    }

    // The live re-read can find the page has lost its sections; the list must not stay up empty.
    [Fact]
    public void Opening_WhenTheReReadFindsTooFew_ClosesAgain() {
        var (bar, _, _) = Bar();
        bar.SetSections(Two());
        bar.SectionsRefreshRequested += () => bar.SetSections([]);

        bar.IsSectionsOpen = true;

        Assert.False(bar.IsSectionsOpen);
    }

    [Fact]
    public void PickingASection_ClosesTheListAndNamesIt() {
        var (bar, _, _) = Bar();
        bar.SetSections(Two());
        bar.IsSectionsOpen = true;
        PageSection? picked = null;
        bar.SectionPicked += s => picked = s;

        bar.Sections[1].SelectCommand.Execute(null);

        Assert.False(bar.IsSectionsOpen);
        Assert.Equal(new PageSection("Beta", 1), picked);
    }

    [Fact]
    public void HandleSectionsShortcut_EscapeWhileOpen_ClosesAndConsumes() {
        var (bar, _, _) = Bar();
        bar.SetSections(Two());
        bar.IsSectionsOpen = true;

        Assert.True(bar.HandleSectionsShortcut(ShortcutId.Escape));
        Assert.False(bar.IsSectionsOpen);
    }

    [Fact]
    public void HandleSectionsShortcut_EscapeWhileClosed_FallsThrough() {
        var (bar, _, _) = Bar();

        Assert.False(bar.HandleSectionsShortcut(ShortcutId.Escape));
    }

    [Fact]
    public void HandleSectionsShortcut_OtherShortcutWhileOpen_ClosesAndStillRuns() {
        var (bar, _, _) = Bar();
        bar.SetSections(Two());
        bar.IsSectionsOpen = true;

        Assert.False(bar.HandleSectionsShortcut(ShortcutId.ShowHelp));
        Assert.False(bar.IsSectionsOpen);
    }

    /// <summary>Enter and the arrows are how a row is reached and picked, so they leave the list up.</summary>
    [Theory]
    [InlineData(ShortcutId.Activate)]
    [InlineData(ShortcutId.SelectNextResult)]
    [InlineData(ShortcutId.SelectPreviousResult)]
    public void HandleSectionsShortcut_KeysThatWorkTheList_LeaveItOpen(ShortcutId id) {
        var (bar, _, _) = Bar();
        bar.SetSections(Two());
        bar.IsSectionsOpen = true;

        Assert.False(bar.HandleSectionsShortcut(id));
        Assert.True(bar.IsSectionsOpen);
    }

    [Theory]
    [InlineData(NavOrientation.Left, PlacementMode.RightEdgeAlignedTop)]
    [InlineData(NavOrientation.Right, PlacementMode.LeftEdgeAlignedTop)]
    [InlineData(NavOrientation.Top, PlacementMode.BottomEdgeAlignedLeft)]
    [InlineData(NavOrientation.Bottom, PlacementMode.TopEdgeAlignedLeft)]
    public void SectionsPlacement_OpensIntoThePage(NavOrientation edge, PlacementMode expected) {
        Assert.Equal(expected, new NavigationViewModel { Orientation = edge }.SectionsPlacement);
    }
}
