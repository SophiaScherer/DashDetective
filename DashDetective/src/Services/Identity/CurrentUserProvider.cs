using DashDetective.Services.Platform.Linux;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Security.Claims;
using System.Security.Principal;

namespace DashDetective.Services.Identity;

/// <summary>
/// Reads the identity of the interactive Windows user backing the current process: the login name,
/// a short initials badge derived from it, and whether the account is an administrator. Values are fixed
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
            // Environment.UserName reads the process token; a denied or broken read is not worth
            // surfacing, and the surface it feeds is a greeting, not a fact about the machine.
            return "User";
        }
    }

    /// <summary>"Administrator" for an account with administrative rights, whether or not this process is
    /// running elevated, "Standard User" for one without, and the neutral "User" where it cannot be told.
    /// </summary>
    private static string ReadRole() {
        try {
            if (OperatingSystem.IsWindows())
                return RoleLabel(ReadWindowsStatus());
            if (OperatingSystem.IsLinux())
                return RoleLabel(ReadLinuxStatus(new ProcFileSystem()));
            return RoleLabel(AdminStatus.Unknown);
        } catch {
            // The token check needs a handle the process may not be allowed to open. Unknown elevation
            // reports the neutral "User" rather than guessing either way — claiming "Standard User"
            // when the check simply failed would be a near-miss.
            return RoleLabel(AdminStatus.Unknown);
        }
    }

    /// <summary>The footer's label for a status. An administrator is one whether or not the process is
    /// elevated: the footer describes the account, and UAC runs an administrator's apps unelevated.</summary>
    internal static string RoleLabel(AdminStatus status) => status switch {
        AdminStatus.Elevated or AdminStatus.Member => "Administrator",
        AdminStatus.Standard => "Standard User",
        _ => "User",
    };

    /// <summary>The well-known SID of the local Administrators group.</summary>
    internal const string AdministratorsSid = "S-1-5-32-544";

    /// <summary>Where the process token stands. A role check alone read every administrator as a standard
    /// user: UAC gives their apps a filtered token in which the Administrators group is deny-only, and a
    /// role check sees only enabled groups. The filtered group is still in the token, as a deny-only SID,
    /// which is what marks the account.</summary>
    [SupportedOSPlatform("windows")]
    private static AdminStatus ReadWindowsStatus() {
        using var identity = WindowsIdentity.GetCurrent();
        var elevated = new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        var denyOnly = identity.Claims.Where(c => c.Type == ClaimTypes.DenyOnlySid).Select(c => c.Value);
        return WindowsStatus(elevated, denyOnly);
    }

    /// <summary>Classifies a Windows token from its enabled-role check and its deny-only group SIDs.</summary>
    internal static AdminStatus WindowsStatus(bool elevated, IEnumerable<string> denyOnlySids) {
        if (elevated)
            return AdminStatus.Elevated;
        return denyOnlySids.Contains(AdministratorsSid, StringComparer.OrdinalIgnoreCase)
            ? AdminStatus.Member
            : AdminStatus.Standard;
    }

    /// <summary>The groups that grant administrative rights on the common distributions: <c>sudo</c> on
    /// Debian and Ubuntu, <c>wheel</c> on Fedora, Arch and SUSE, <c>admin</c> on older Ubuntu.</summary>
    internal static readonly IReadOnlySet<string> LinuxAdminGroups =
        new HashSet<string>(StringComparer.Ordinal) { "sudo", "wheel", "admin" };

    /// <summary>Linux has no token elevation, so the closest defined meaning is used: root is elevated, a
    /// member of an administrative group is an administrator, anyone else is a standard user. The
    /// process's own groups come from <c>/proc/self/status</c>, not from <c>/etc/group</c>'s member lists,
    /// which omit a user's primary group.</summary>
    internal static AdminStatus ReadLinuxStatus(IProcFileSystem proc) {
        // Concatenated forward-slash literals, never Path.Combine — see IProcFileSystem.
        var status = proc.ReadAllLines("/proc/self/status");
        var uid = ProcPidStatusParser.Parse(status).Uid;
        if (uid is null)
            return AdminStatus.Unknown;
        if (uid == 0)
            return AdminStatus.Elevated;

        var groups = ProcPidStatusParser.ParseGroups(status);
        if (groups is null)
            return AdminStatus.Unknown;

        // An unreadable group file says nothing either way, and "Standard User" would be a near-miss.
        var groupFile = proc.ReadAllLines("/etc/group");
        if (groupFile.Count == 0)
            return AdminStatus.Unknown;

        var adminGids = EtcGroupParser.GidsNamed(groupFile, LinuxAdminGroups);
        return groups.Any(adminGids.Contains) ? AdminStatus.Member : AdminStatus.Standard;
    }

    /// <summary>Up to two uppercase letters for the avatar badge: the first letter of the first two
    /// name tokens (split on space/<c>.</c>/<c>_</c>/<c>-</c>), else the first two letters of the name,
    /// else "?".</summary>
    internal static string DeriveInitials(string name) {
        var tokens = name.Split(new[] { ' ', '.', '_', '-' }, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length >= 2)
            return $"{char.ToUpperInvariant(tokens[0][0])}{char.ToUpperInvariant(tokens[1][0])}";

        var single = tokens.Length == 1 ? tokens[0] : name.Trim();
        if (single.Length >= 2)
            return single[..2].ToUpperInvariant();
        return single.Length == 1 ? single.ToUpperInvariant() : "?";
    }
}

/// <summary>Where the account stands: running elevated (or as root), an administrator running
/// unelevated, a standard user, or not known.</summary>
internal enum AdminStatus {
    Unknown,
    Standard,
    Member,
    Elevated,
}

/// <summary>Immutable snapshot of the interactive user's identity for the nav footer.</summary>
public readonly record struct CurrentUserInfo(string DisplayName, string Initials, string Role);
