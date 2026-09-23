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

    /// <summary>Windows 11's own metrics: three 46px buttons in a 32px band.</summary>
    [Fact]
    public void CaptionReserve_IsThreeButtonsWide() =>
        Assert.Equal(138, TitleBarRules.CaptionReserve);
}
