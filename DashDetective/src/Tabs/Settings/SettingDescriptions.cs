using System;

namespace DashDetective.Tabs.Settings;

/// <summary>
/// The setting descriptions that cannot be shared across platforms, because they name the mechanism
/// rather than the effect. "Start with Windows" is wrong on a machine that has no Windows to start with,
/// and it is the line the toggle shows <i>and</i> the line universal search matches on, so leaving it
/// would make the setting both wrong and hard to find.
///
/// Only the varying strings live here; everything else stays a literal on <see cref="SettingCatalog"/>.
/// The platform arrives as an explicit <c>windows</c> parameter so both are testable from either host —
/// the <c>ProcessGroupNames</c> shape.
/// </summary>
internal static class SettingDescriptions {
    /// <summary>The "Launch at startup" explanation for this machine.</summary>
    internal static string LaunchAtStartup { get; } = LaunchAtStartupFor(OperatingSystem.IsWindows());

    internal static string LaunchAtStartupFor(bool windows) =>
        windows ? "Start with Windows" : "Start when you log in";
}
