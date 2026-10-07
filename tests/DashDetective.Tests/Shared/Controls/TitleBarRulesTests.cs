using Avalonia;
using Avalonia.Platform;
using DashDetective.Shared.Controls;
using Xunit;

namespace DashDetective.Tests.Shared.Controls;

/// <summary>
/// Covers <see cref="TitleBarRules"/>: which windows extend into their title bar, when the bar is drawn,
/// and how it lines up with the caption buttons. The platform is passed in, so every arm runs on every
/// CI leg.
/// </summary>
public class TitleBarRulesTests {
    [Fact]
    public void ShouldExtend_WindowsWithoutAContrastTheme_Extends() =>
        Assert.True(TitleBarRules.ShouldExtend(isWindows: true, ColorContrastPreference.NoPreference));

    /// <summary>A Windows contrast theme hands the caption back to the OS, which draws it in the user's
    /// own contrast colors.</summary>
    [Fact]
    public void ShouldExtend_WindowsContrastTheme_KeepsTheSystemTitleBar() =>
        Assert.False(TitleBarRules.ShouldExtend(isWindows: true, ColorContrastPreference.High));

    /// <summary>Linux and macOS keep the title bar their desktop draws.</summary>
    [Theory]
    [InlineData(ColorContrastPreference.NoPreference)]
    [InlineData(ColorContrastPreference.High)]
    public void ShouldExtend_OffWindows_NeverExtends(ColorContrastPreference contrast) =>
        Assert.False(TitleBarRules.ShouldExtend(isWindows: false, contrast));

    [Fact]
    public void IsShown_ExtendedWithACaptionBand_IsShown() =>
        Assert.True(TitleBarRules.IsShown(extended: true, new Thickness(0, 32, 0, 0)));

    /// <summary>The platform can refuse to extend, and then the system title bar is already there.</summary>
    [Fact]
    public void IsShown_NotExtended_IsHidden() =>
        Assert.False(TitleBarRules.IsShown(extended: false, new Thickness(0, 32, 0, 0)));

    /// <summary>No band means nothing to line up with, as in full screen.</summary>
    [Fact]
    public void IsShown_NoCaptionBand_IsHidden() =>
        Assert.False(TitleBarRules.IsShown(extended: true, default));

    /// <summary>The bar is exactly as tall as the band the caption buttons are drawn in.</summary>
    [Fact]
    public void Height_MatchesTheReportedCaptionBand() =>
        Assert.Equal(32, TitleBarRules.Height(new Thickness(0, 32, 0, 0), default));

    /// <summary>What sits off screen when maximized is added, so the band stays whole on screen.</summary>
    [Fact]
    public void Height_WithAnOffScreenMargin_GrowsByIt() =>
        Assert.Equal(40, TitleBarRules.Height(new Thickness(0, 32, 0, 0), new Thickness(8)));

    [Fact]
    public void Padding_ReservesTheCaptionButtonsAtTheTrailingEdge() =>
        Assert.Equal(new Thickness(0, 0, TitleBarRules.CaptionReserve, 0), TitleBarRules.Padding(default));

    [Fact]
    public void Padding_WithAnOffScreenMargin_InsetsTheContentByIt() =>
        Assert.Equal(new Thickness(8, 8, 8 + TitleBarRules.CaptionReserve, 0),
                     TitleBarRules.Padding(new Thickness(8)));

    /// <summary>The bar's height comes out of the client area only while it is drawn.</summary>
    [Fact]
    public void Reserved_Shown_IsTheBarsHeight() =>
        Assert.Equal(32, TitleBarRules.Reserved(extended: true, new Thickness(0, 32, 0, 0), default));

    /// <summary>Off Windows the system title bar sits outside the client area, so reserving room for a bar
    /// there would raise the window minimum for nothing.</summary>
    [Fact]
    public void Reserved_NotExtended_IsNothing() =>
        Assert.Equal(0, TitleBarRules.Reserved(extended: false, new Thickness(0, 32, 0, 0), default));

    /// <summary>The taskbar's "Close window", its thumbnail × and Alt+F4 all arrive as this.</summary>
    [Fact]
    public void IsSystemClose_ScClose_IsClose() =>
        Assert.True(TitleBarRules.IsSystemClose(0x0112, 0xF060));

    /// <summary>The low four bits are the system's own and are masked off, as Windows says to.</summary>
    [Fact]
    public void IsSystemClose_ScCloseWithLowBitsSet_IsClose() =>
        Assert.True(TitleBarRules.IsSystemClose(0x0112, 0xF063));

    /// <summary>Every other system command — minimize, maximize, the keyboard menu — is left alone.</summary>
    [Theory]
    [InlineData(0xF020)]
    [InlineData(0xF030)]
    [InlineData(0xF100)]
    public void IsSystemClose_OtherCommand_IsNotClose(int command) =>
        Assert.False(TitleBarRules.IsSystemClose(0x0112, command));

    /// <summary>The same number in another message is not a command at all.</summary>
    [Fact]
    public void IsSystemClose_OtherMessage_IsNotClose() =>
        Assert.False(TitleBarRules.IsSystemClose(0x0010, 0xF060));
}
