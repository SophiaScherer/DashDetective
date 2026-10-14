using Avalonia.Input;
using DashDetective.Services.Theming;
using DashDetective.Shared.Shortcuts;
using DashDetective.Shell;
using DashDetective.Shell.Help;
using DashDetective.Tabs.FileExplorer;
using DashDetective.Tabs.Settings;
using DashDetective.Tests.Fakes;
using Xunit;

namespace DashDetective.Tests.Shell;

/// <summary>
/// Covers <see cref="MouseNavigation"/>, the shell's rule for the mouse's back and forward buttons. A
/// capture leaves the press alone, an open modal swallows it, a focused text box keeps it (the guard
/// Alt+← has), the current tab steps first, and only back falls through to page history.
/// </summary>
public class MouseNavigationTests {
    private readonly HelpViewModel _help = new(new ShortcutBindings());
    private readonly AccentPickerViewModel _picker = new(new ThemeService(), () => { });
    private int _pageBacks;

    /// <summary>Page history with somewhere to go, counting the steps taken.</summary>
    private bool GoBack() {
        _pageBacks++;
        return true;
    }

    private bool Press(PointerUpdateKind button, IShortcutTarget? page,
                       bool textInputFocused = false, bool capturing = false) =>
        MouseNavigation.Handle(button, textInputFocused, capturing, _help, _picker, page, GoBack);

    [Fact]
    public void Handle_BackWithTabHistory_StepsTheTabNotThePage() {
        var page = new FakeShortcutTarget(ShortcutId.NavigateBack);

        Assert.True(Press(PointerUpdateKind.XButton1Pressed, page));
        Assert.Equal([ShortcutId.NavigateBack], page.Offered);
        Assert.Equal(0, _pageBacks);
    }

    [Fact]
    public void Handle_BackWhenTheTabDeclines_GoesBackAPage() {
        var page = new FakeShortcutTarget();

        Assert.True(Press(PointerUpdateKind.XButton1Pressed, page));
        Assert.Equal(1, _pageBacks);
    }

    [Fact]
    public void Handle_BackOnAPageWithNoShortcuts_GoesBackAPage() {
        Assert.True(Press(PointerUpdateKind.XButton1Pressed, page: null));
        Assert.Equal(1, _pageBacks);
    }

    [Fact]
    public void Handle_BackAtTheStartOfHistory_IsUnconsumed() =>
        Assert.False(MouseNavigation.Handle(PointerUpdateKind.XButton1Pressed, false, false, _help, _picker,
                                            new FakeShortcutTarget(), () => false));

    [Fact]
    public void Handle_BackWithATextBoxFocused_DoesNothing() {
        var page = new FakeShortcutTarget(ShortcutId.NavigateBack);

        Assert.False(Press(PointerUpdateKind.XButton1Pressed, page, textInputFocused: true));
        Assert.Empty(page.Offered);
        Assert.Equal(0, _pageBacks);
    }

    /// <summary>The case that motivated the guard: File Explorer declines its folder back while the path box
    /// is open, so without it the press fell through and left the page mid-edit.</summary>
    [Fact]
    public void Handle_BackWhileEditingFileExplorersPath_StaysOnThePage() {
        using var explorer = new FileExplorerViewModel(new FakeShellInterop(), new UnsupportedFileSystemRoots());
        Assert.True(explorer.HandleShortcut(ShortcutId.FocusAddressBar));

        Assert.False(Press(PointerUpdateKind.XButton1Pressed, explorer, textInputFocused: true));
        Assert.True(explorer.IsPathEditing);
        Assert.Equal(0, _pageBacks);
    }

    [Fact]
    public void Handle_BackWithHelpOpen_IsSwallowedAndNavigatesNothing() {
        var page = new FakeShortcutTarget(ShortcutId.NavigateBack);
        _help.Open();

        Assert.True(Press(PointerUpdateKind.XButton1Pressed, page));
        Assert.True(_help.IsOpen);
        Assert.Empty(page.Offered);
        Assert.Equal(0, _pageBacks);
    }

    [Fact]
    public void Handle_BackWhileCapturingAShortcut_IsLeftAlone() {
        var page = new FakeShortcutTarget(ShortcutId.NavigateBack);

        Assert.False(Press(PointerUpdateKind.XButton1Pressed, page, capturing: true));
        Assert.Empty(page.Offered);
        Assert.Equal(0, _pageBacks);
    }

    [Fact]
    public void Handle_ForwardWithTabHistory_StepsTheTab() {
        var page = new FakeShortcutTarget(ShortcutId.NavigateForward);

        Assert.True(Press(PointerUpdateKind.XButton2Pressed, page));
        Assert.Equal([ShortcutId.NavigateForward], page.Offered);
    }

    /// <summary>There is no page forward, so a declined forward press goes nowhere.</summary>
    [Fact]
    public void Handle_ForwardWhenTheTabDeclines_NeverTouchesPageHistory() {
        Assert.False(Press(PointerUpdateKind.XButton2Pressed, new FakeShortcutTarget()));
        Assert.Equal(0, _pageBacks);
    }

    [Theory]
    [InlineData(PointerUpdateKind.LeftButtonPressed)]
    [InlineData(PointerUpdateKind.RightButtonPressed)]
    [InlineData(PointerUpdateKind.MiddleButtonPressed)]
    public void Handle_OtherButtons_AreIgnored(PointerUpdateKind button) {
        var page = new FakeShortcutTarget(ShortcutId.NavigateBack);

        Assert.False(Press(button, page));
        Assert.Empty(page.Offered);
        Assert.Equal(0, _pageBacks);
    }
}
