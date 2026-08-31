using DashDetective.Shared;
using DashDetective.Shared.Shortcuts;
using DashDetective.Shell.Help;
using System;
using System.Linq;
using Xunit;

namespace DashDetective.Tests.Shell.Help;

/// <summary>Covers <see cref="HelpViewModel"/>: the open/closed state the overlay binds its visibility
/// to, and that the copy is surfaced from <see cref="HelpContent"/> rather than duplicated.</summary>
public class HelpViewModelTests {
    [Fact]
    public void IsOpen_DefaultsToClosed() {
        Assert.False(new HelpViewModel(new ShortcutBindings()).IsOpen);
    }

    [Fact]
    public void Open_ShowsTheModal() {
        var vm = new HelpViewModel(new ShortcutBindings());
        vm.Open();
        Assert.True(vm.IsOpen);
    }

    [Fact]
    public void Close_HidesTheModal() {
        var vm = new HelpViewModel(new ShortcutBindings()) { IsOpen = true };
        vm.Close();
        Assert.False(vm.IsOpen);
    }

    [Fact]
    public void Open_WhenAlreadyOpen_StaysOpen() {
        var vm = new HelpViewModel(new ShortcutBindings());
        vm.Open();
        vm.Open();
        Assert.True(vm.IsOpen);
    }

    [Fact]
    public void Content_ComesFromHelpContent() {
        var vm = new HelpViewModel(new ShortcutBindings());
        Assert.Equal(HelpContent.Description, vm.Description);
        Assert.Same(HelpContent.Tips, vm.Tips);
        Assert.Same(HelpContent.GettingStarted, vm.GettingStarted);
    }

    [Fact]
    public void VersionText_CarriesTheProductNameAndVersion() {
        var vm = new HelpViewModel(new ShortcutBindings());
        Assert.Contains(AppInfo.Name, vm.VersionText);
        Assert.Contains(AppInfo.Version, vm.VersionText);
    }

    [Fact]
    public void SelectedTab_DefaultsToOverview() {
        Assert.Equal(HelpTab.Overview, new HelpViewModel(new ShortcutBindings()).SelectedTab);
    }

    [Fact]
    public void Tabs_CoverEverySection() {
        var vm = new HelpViewModel(new ShortcutBindings());
        Assert.Equal(Enum.GetValues<HelpTab>(), vm.Tabs.Select(tab => tab.Value));
    }

    [Fact]
    public void SelectingATab_MarksOnlyThatOne() {
        var vm = new HelpViewModel(new ShortcutBindings());
        vm.Tabs.Single(tab => tab.Value == HelpTab.Tips).SelectCommand.Execute(null);

        Assert.Equal(HelpTab.Tips, vm.SelectedTab);
        Assert.Single(vm.Tabs, tab => tab.IsSelected);
        Assert.True(vm.Tabs.Single(tab => tab.Value == HelpTab.Tips).IsSelected);
    }

    [Fact]
    public void Overview_ShowsEverySection() {
        var vm = new HelpViewModel(new ShortcutBindings()) { SelectedTab = HelpTab.Overview };
        Assert.True(vm.ShowGettingStarted);
        Assert.True(vm.ShowTips);
        Assert.True(vm.ShowShortcuts);
    }

    [Theory]
    [InlineData(HelpTab.GettingStarted, true, false, false)]
    [InlineData(HelpTab.Tips, false, true, false)]
    [InlineData(HelpTab.Shortcuts, false, false, true)]
    public void ASectionTab_ShowsOnlyItsOwnSection(
        HelpTab tab, bool gettingStarted, bool tips, bool shortcuts) {
        var vm = new HelpViewModel(new ShortcutBindings()) { SelectedTab = tab };
        Assert.Equal(gettingStarted, vm.ShowGettingStarted);
        Assert.Equal(tips, vm.ShowTips);
        Assert.Equal(shortcuts, vm.ShowShortcuts);
    }

    /// <summary>Opening Help is a request for everything it knows, not for wherever the last visit
    /// left off.</summary>
    [Fact]
    public void Open_ReturnsToOverview() {
        var vm = new HelpViewModel(new ShortcutBindings()) { SelectedTab = HelpTab.Shortcuts };
        vm.Close();
        vm.Open();
        Assert.Equal(HelpTab.Overview, vm.SelectedTab);
    }
}
