using System;

namespace DashDetective.Services.Identity;

/// <summary>
/// Reads the account picture the operating system holds for the interactive user — what Windows shows on
/// the sign-in screen and Start menu, and what a Linux desktop shows in its session menu.
///
/// Returns the file's <b>encoded</b> bytes (PNG/JPEG as stored) rather than a decoded image, so this stays
/// free of any UI type and can be tested without a render backend. Implementations must never throw: no
/// picture, a denied read, or a file too large to be a portrait all yield <c>null</c>, and the footer
/// falls back to the initials badge it drew before.
/// </summary>
internal interface IUserPictureProvider {
    /// <summary>The account picture's encoded bytes, or <c>null</c> when there is none to show.</summary>
    byte[]? Read();

    /// <summary>The reader for this machine — the only place the platform is decided for this seam.</summary>
    static IUserPictureProvider ForCurrentPlatform() =>
        OperatingSystem.IsWindows() ? new WindowsUserPictureProvider()
        : OperatingSystem.IsLinux() ? new LinuxUserPictureProvider()
        : new UnsupportedUserPictureProvider();
}
