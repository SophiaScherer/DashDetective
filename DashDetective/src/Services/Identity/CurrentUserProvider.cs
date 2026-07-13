using System;
using System.Runtime.Versioning;
using System.Security.Principal;

namespace DashDetective.Services.Identity;

/// <summary>
/// Reads the identity of the interactive Windows user backing the current process: the login name,
/// a short initials badge derived from it, and the account's real privilege level. Values are fixed
/// for the lifetime of the session, so this is a plain static reader (matching
/// <c>SystemInfoProvider</c>) with no observable state. Every source degrades independently and never
/// throws — a locked-down or non-Windows host yields sensible fallbacks rather than crashing the shell.
/// </summary>
public static class CurrentUserProvider {
    /// <summary>Reads the current user's identity. Safe to call on any platform.</summary>
    public static CurrentUserInfo Load() {
        var name = ReadUserName();
        return new CurrentUserInfo(name, DeriveInitials(name), ReadRole());
    }

    /// <summary>The interactive user's login name, e.g. "sophiasch". Falls back to "User" if unknown.</summary>
    private static string ReadUserName() {
        try {
            var name = Environment.UserName;
            return string.IsNullOrWhiteSpace(name) ? "User" : name.Trim();
        } catch {
            return "User";
        }
    }

    /// <summary>"Administrator" when the process token is elevated into the local Administrators role,
    /// otherwise "Standard User". Non-Windows or a denied token check yields "User".</summary>
    private static string ReadRole() {
        if (!OperatingSystem.IsWindows())
            return "User";

        try {
            return IsAdministrator() ? "Administrator" : "Standard User";
        } catch {
            return "User";
        }
    }

    [SupportedOSPlatform("windows")]
    private static bool IsAdministrator() {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    /// <summary>Up to two uppercase letters for the avatar badge: the first letter of the first two
    /// name tokens (split on space/<c>.</c>/<c>_</c>/<c>-</c>), else the first two letters of the name,
    /// else "?".</summary>
    private static string DeriveInitials(string name) {
        var tokens = name.Split(new[] { ' ', '.', '_', '-' }, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length >= 2)
            return $"{char.ToUpperInvariant(tokens[0][0])}{char.ToUpperInvariant(tokens[1][0])}";

        var single = tokens.Length == 1 ? tokens[0] : name.Trim();
        if (single.Length >= 2)
            return single[..2].ToUpperInvariant();
        return single.Length == 1 ? single.ToUpperInvariant() : "?";
    }
}

/// <summary>Immutable snapshot of the interactive user's identity for the nav footer.</summary>
public readonly record struct CurrentUserInfo(string DisplayName, string Initials, string Role);
