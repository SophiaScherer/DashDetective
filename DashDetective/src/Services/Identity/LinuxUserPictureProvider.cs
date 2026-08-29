using System;
using System.IO;

namespace DashDetective.Services.Identity;

/// <summary>
/// Reads the account picture a Linux desktop shows for the current user. Two conventions cover
/// essentially every desktop: <c>~/.face</c> (the long-standing per-user file, which GNOME and KDE both
/// honour) and the AccountsService icon the display manager caches per user name. The home file is tried
/// first — it is what the user set most recently, and AccountsService may hold a copy that is stale.
/// Portable managed <c>System.IO</c>, so no <c>[SupportedOSPlatform]</c>.
/// </summary>
internal sealed class LinuxUserPictureProvider : IUserPictureProvider {
    private const string AccountsServiceIcons = "/var/lib/AccountsService/icons";

    private readonly string _home;
    private readonly string _accountsServiceIcons;

    public LinuxUserPictureProvider()
        : this(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), AccountsServiceIcons) { }

    /// <summary>Test seam: injects both roots so the lookup runs against a temp folder from any dev
    /// machine, the way <c>LinuxStartupRegistration</c> injects its autostart directory.</summary>
    internal LinuxUserPictureProvider(string home, string accountsServiceIcons) {
        _home = home;
        _accountsServiceIcons = accountsServiceIcons;
    }

    public byte[]? Read() {
        foreach (var path in CandidatePaths())
            if (UserPictureFile.TryRead(path) is { } bytes)
                return bytes;

        return null;
    }

    /// <summary>Real filesystem paths, so Path.Combine is right here — the never-Path.Combine rule is
    /// about /proc and /sys literals, which must stay forward-slashed to match the fixtures.</summary>
    private string[] CandidatePaths() {
        var user = Environment.UserName;
        return [
            Path.Combine(_home, ".face"),
            Path.Combine(_home, ".face.icon"),
            string.IsNullOrWhiteSpace(user) ? "" : Path.Combine(_accountsServiceIcons, user),
        ];
    }
}
