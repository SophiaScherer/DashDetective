using DashDetective.Shared;
using System;

namespace DashDetective.Tabs.Settings;

/// <summary>
/// The setting descriptions that name a mechanism rather than an effect, so cannot be shared. "Start
/// with Windows" is both the line the toggle shows and the line universal search matches, so leaving it
/// elsewhere would make the setting wrong and hard to find at once. Only the varying strings live here;
/// the platform arrives as a parameter so both arms are testable from either host, the
/// <c>ProcessGroupNames</c> shape.
/// </summary>
internal static class SettingDescriptions {
    /// <summary>The "Launch at startup" explanation for this machine.</summary>
    internal static string LaunchAtStartup { get; } = LaunchAtStartupFor(OperatingSystem.IsWindows());

    internal static string LaunchAtStartupFor(bool windows) =>
        windows ? "Start with Windows" : "Start when you log in";

    /// <summary>Where the tray cannot be honoured the row says what closing actually does, rather than
    /// describing a behaviour its disabled toggle will never produce.</summary>
    internal static string ShowInTray { get; } = ShowInTrayFor(TrayIntegration.HidesOnClose);

    internal static string ShowInTrayFor(bool hidesOnClose) =>
        hidesOnClose
            ? "Keep console running in background"
            : "Not available on this desktop — closing exits the app";
}
