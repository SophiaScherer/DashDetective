using System;
using System.Globalization;

namespace DashDetective.Services.Platform.Linux;

/// <summary>
/// A process's display name, from <c>/proc/[pid]/cmdline</c> falling back to the kernel's <c>comm</c>.
/// A shared derivation rather than a parser — the Processes tab names every process and the Network tab
/// names each connection's owner, so sharing it is what stops the same process reading
/// <c>systemd-resolved</c> on one tab and the 15-char-truncated <c>systemd-resolve</c> on the other.
/// The <see cref="CpuFacts"/> precedent.
///
/// <b>An unnamed process yields <c>""</c>, not a placeholder</b>, because the two consumers' placeholders
/// differ: the Processes tab shows "Unknown", while the Network tab prefers "PID 1234" — which it could
/// not produce if this had already substituted something.
///
/// <b>No <c>.exe</c> is appended.</b> That suffix belongs to the Windows providers.
/// </summary>
internal static class ProcPidName {
    // Concatenated forward-slash literals, never Path.Combine — see IProcFileSystem.
    private const string ProcRoot = "/proc/";

    /// <summary>The name for a PID, reading both sources itself. For callers that have not already parsed
    /// <c>stat</c> — the ones that want a name and nothing else.</summary>
    internal static string Read(IProcFileSystem proc, int pid) {
        var root = ProcRoot + pid.ToString(CultureInfo.InvariantCulture) + "/";
        return From(proc.ReadAllText(root + "cmdline"), proc.ReadAllText(root + "comm")?.Trim() ?? "");
    }

    /// <summary>The name from sources the caller already holds. <c>cmdline</c> is preferred because
    /// <c>comm</c> truncates at 15 characters.</summary>
    internal static string From(string? cmdline, string comm) {
        var name = Basename(FirstArgument(cmdline));
        return name.Length > 0 ? name : comm;
    }

    /// <summary>Whether the process has a user-space command line at all. Empty means a kernel thread or a
    /// zombie, which is one of the process classifier's inputs.</summary>
    internal static bool HasCommandLine(string? cmdline) => FirstArgument(cmdline).Length > 0;

    /// <summary><c>cmdline</c> holds NUL-separated arguments, so argv[0] ends at the first NUL — splitting
    /// on spaces would mangle any path containing one.</summary>
    private static string FirstArgument(string? cmdline) {
        if (string.IsNullOrEmpty(cmdline))
            return "";

        var nul = cmdline.IndexOf('\0');
        return (nul < 0 ? cmdline : cmdline[..nul]).Trim();
    }

    private static string Basename(string path) {
        var slash = path.LastIndexOf('/');
        return slash < 0 ? path : path[(slash + 1)..];
    }
}
