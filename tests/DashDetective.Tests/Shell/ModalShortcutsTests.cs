using DashDetective.Services.Theming;
using DashDetective.Shared.Shortcuts;
using DashDetective.Shell;
using DashDetective.Shell.Help;
using DashDetective.Tabs.Settings;
using System;
using System.Linq;
using Xunit;

namespace DashDetective.Tests.Shell;

/// <summary>
/// Covers <see cref="ModalShortcuts"/>, the modal step of the shell's shortcut chain. While Help or the
/// accent picker is open, Esc dismisses it, Enter falls through to the focused button, every other shortcut
/// is swallowed, and keys resolve in the Global scope whatever sits behind the scrim.
/// </summary>
public class ModalShortcutsTests {
    private static (HelpViewModel Help, AccentPickerViewModel Picker) Create() =>
        (new HelpViewModel(new ShortcutBindings()), new AccentPickerViewModel(new ThemeService(), () => { }));

    /// <summary>Every shortcut the modals neither dismiss on nor pass through.</summary>
    public static TheoryData<ShortcutId> SwallowedShortcuts {
        get {
            var data = new TheoryData<ShortcutId>();
            foreach (var id in Enum.GetValues<ShortcutId>().Except([ShortcutId.Escape, ShortcutId.Activate]))
                data.Add(id);
            return data;
        }
    }

    // ----- No modal -----

    [Theory]
    [InlineData(ShortcutId.Escape)]
    [InlineData(ShortcutId.Activate)]
    [InlineData(ShortcutId.NavigateTab1)]
    public void Handle_NoModalOpen_LeavesTheKeyToTheRestOfTheChain(ShortcutId id) {
        var (help, picker) = Create();
        Assert.Null(ModalShortcuts.Handle(id, help, picker));
    }

    [Fact]
    public void Scope_NoModalOpen_IsWhateverSitsUnderneath() {
        var (help, picker) = Create();
        Assert.Equal(ShortcutScope.Processes, ModalShortcuts.Scope(help, picker, ShortcutScope.Processes));
        Assert.Equal(ShortcutScope.Search, ModalShortcuts.Scope(help, picker, ShortcutScope.Search));
    }

    // ----- Help -----

    [Fact]
    public void Handle_HelpOpen_EscapeClosesHelp() {
        var (help, picker) = Create();
        help.Open();

        Assert.True(ModalShortcuts.Handle(ShortcutId.Escape, help, picker));
        Assert.False(help.IsOpen);
    }

    [Fact]
    public void Handle_HelpOpen_ActivateFallsThroughToTheFocusedButton() {
        var (help, picker) = Create();
        help.Open();

        Assert.False(ModalShortcuts.Handle(ShortcutId.Activate, help, picker));
        Assert.True(help.IsOpen);
    }

    [Theory]
    [MemberData(nameof(SwallowedShortcuts))]
    public void Handle_HelpOpen_SwallowsEveryOtherShortcut(ShortcutId id) {
        var (help, picker) = Create();
        help.Open();

        Assert.True(ModalShortcuts.Handle(id, help, picker));
        Assert.True(help.IsOpen);
    }

    [Theory]
    [InlineData(ShortcutScope.Global)]
    [InlineData(ShortcutScope.Search)]
    [InlineData(ShortcutScope.Processes)]
    [InlineData(ShortcutScope.FileExplorer)]
    public void Scope_HelpOpen_IsGlobal(ShortcutScope underneath) {
        var (help, picker) = Create();
        help.Open();

        Assert.Equal(ShortcutScope.Global, ModalShortcuts.Scope(help, picker, underneath));
    }

    // ----- Accent picker -----

    [Fact]
    public void Handle_PickerOpen_EscapeCancelsTheDraft() {
        var (help, picker) = Create();
        picker.OpenCommand.Execute(null);
        picker.HexText = "#1a3a8a";

        Assert.True(ModalShortcuts.Handle(ShortcutId.Escape, help, picker));
        Assert.False(picker.IsOpen);
        Assert.False(picker.IsDirty);
    }

    [Fact]
    public void Handle_PickerOpen_ActivateFallsThroughAndKeepsTheDraft() {
        var (help, picker) = Create();
        picker.OpenCommand.Execute(null);
        picker.HexText = "#1a3a8a";

        Assert.False(ModalShortcuts.Handle(ShortcutId.Activate, help, picker));
        Assert.True(picker.IsOpen);
        Assert.True(picker.IsDirty);
    }

    [Theory]
    [MemberData(nameof(SwallowedShortcuts))]
    public void Handle_PickerOpen_SwallowsEveryOtherShortcut(ShortcutId id) {
        var (help, picker) = Create();
        picker.OpenCommand.Execute(null);

        Assert.True(ModalShortcuts.Handle(id, help, picker));
        Assert.True(picker.IsOpen);
        Assert.False(help.IsOpen);
    }

    [Theory]
    [InlineData(ShortcutScope.Global)]
    [InlineData(ShortcutScope.Search)]
    [InlineData(ShortcutScope.Processes)]
    [InlineData(ShortcutScope.FileExplorer)]
    public void Scope_PickerOpen_IsGlobal(ShortcutScope underneath) {
        var (help, picker) = Create();
        picker.OpenCommand.Execute(null);

        Assert.Equal(ShortcutScope.Global, ModalShortcuts.Scope(help, picker, underneath));
    }
}
