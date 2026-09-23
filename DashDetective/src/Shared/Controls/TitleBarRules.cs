using Avalonia;
using Avalonia.Platform;

namespace DashDetective.Shared.Controls;

/// <summary>
/// When the custom title bar is drawn and how it is laid out, kept out of the view so it is testable
/// without a window. The numbers are Windows 11's own caption metrics, and the caption-button theme in
/// WindowChrome.axaml reads them from here, so the bar and the buttons cannot disagree.
/// </summary>
public static class TitleBarRules {
    /// <summary>The caption band's height, as Windows 11 draws it for an app with a custom title bar.</summary>
    public const double DefaultHeight = 32;

    /// <summary>One caption button's width, as Windows 11 draws it.</summary>
    public const double CaptionButtonWidth = 46;

    /// <summary>Minimize, maximize/restore and close.</summary>
    public const int CaptionButtonCount = 3;

    /// <summary>The room the caption buttons take at the bar's trailing edge.</summary>
    public const double CaptionReserve = CaptionButtonWidth * CaptionButtonCount;

    /// <summary>Whether a window should extend into its title bar at all. Windows only: other platforms
    /// keep their own title bar. And not under a Windows contrast theme, where the OS-drawn caption
    /// follows the user's contrast colors and ours would not.</summary>
    public static bool ShouldExtend(bool isWindows, ColorContrastPreference contrast) =>
        isWindows && contrast != ColorContrastPreference.High;

    /// <summary>Whether the bar is drawn: only while the window really is extended, which the platform
    /// can refuse, and while there is a caption band to line up with.</summary>
    public static bool IsShown(bool extended, Thickness decorationMargin) =>
        extended && decorationMargin.Top > 0;

    /// <summary>The bar's height: the caption band the platform reports, plus whatever of the window
    /// sits off screen above it when maximized.</summary>
    public static double Height(Thickness decorationMargin, Thickness offScreen) =>
        decorationMargin.Top + offScreen.Top;

    /// <summary>Keeps the bar's content on screen and clear of the caption buttons.</summary>
    public static Thickness Padding(Thickness offScreen) =>
        new(offScreen.Left, offScreen.Top, offScreen.Right + CaptionReserve, 0);
}
