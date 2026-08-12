using System;

namespace DashDetective.Services.Startup;

/// <summary>
/// Reads and writes whether the app launches with the user's session. Implementations must never
/// throw: a denied write or an unavailable store degrades to "not enabled", so a failure never
/// propagates into the Settings toggle.
/// </summary>
internal interface IStartupRegistration {
    /// <summary>Whether a startup entry for this app currently exists.</summary>
    bool IsEnabled();

    /// <summary>Adds or removes the startup entry.</summary>
    void SetEnabled(bool enabled);

    /// <summary>The registration for this machine — the <c>Run</c> key on Windows, an XDG autostart
    /// entry on Linux, and one that reports "not enabled" and ignores writes anywhere else (what the old
    /// inline platform guards did).</summary>
    static IStartupRegistration ForCurrentPlatform() =>
        OperatingSystem.IsWindows() ? new WindowsStartupRegistration()
        : OperatingSystem.IsLinux() ? new LinuxStartupRegistration()
        : new UnsupportedStartupRegistration();
}
