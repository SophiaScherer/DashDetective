using DashDetective.Shared;
using DashDetective.Shared.Shortcuts;
using DashDetective.Shell.Help;
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
    }

    [Fact]
    public void VersionText_CarriesTheProductNameAndVersion() {
        var vm = new HelpViewModel(new ShortcutBindings());
        Assert.Contains(AppInfo.Name, vm.VersionText);
        Assert.Contains(AppInfo.Version, vm.VersionText);
    }
}
