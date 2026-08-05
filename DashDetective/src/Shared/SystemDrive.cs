using System;
using System.IO;

namespace DashDetective.Shared;

/// <summary>
/// Where the OS itself is installed. Centralises the
/// <c>Path.GetPathRoot(Environment.SystemDirectory)</c> idiom so labels don't hardcode "C:".
///
/// Two shapes, because the platforms name the same thing differently: <see cref="Letter"/> is the
/// Windows drive letter that volume records are keyed by, and <see cref="Root"/> is the rooted path
/// that works everywhere. Neither can change while the process runs, so both resolve once.
/// </summary>
public static class SystemDrive {
    /// <summary>The system drive's letter (e.g. 'C'), falling back to 'C' when it can't be resolved.
    /// Windows-shaped by nature — there are no drive letters elsewhere — so it is for matching a
    /// volume record's letter, not for building a path. Use <see cref="Root"/> for that.</summary>
    public static char Letter { get; } = ReadLetter();

    /// <summary>The system drive's root as a path (<c>C:\</c> on Windows, <c>/</c> elsewhere), for
    /// anything that has to open or measure it. Never empty, so a caller can hand it straight to
    /// <c>DriveInfo</c> or a capacity label.</summary>
    public static string Root { get; } = ReadRoot();

    private static char ReadLetter() {
        try {
            var root = Path.GetPathRoot(Environment.SystemDirectory);
            if (!string.IsNullOrEmpty(root) && char.IsLetter(root[0]))
                return char.ToUpperInvariant(root[0]);
        } catch {
            // Unreadable system directory (or a non-Windows host) — fall through to the default.
        }
        return 'C';
    }

    private static string ReadRoot() {
        // Off Windows there is one root and SystemDirectory is empty, so don't ask.
        if (!OperatingSystem.IsWindows())
            return "/";

        try {
            if (Path.GetPathRoot(Environment.SystemDirectory) is { Length: > 0 } root)
                return root;
        } catch {
            // Unreadable system directory — fall through to the drive letter we already resolved.
        }
        return $@"{Letter}:\";
    }
}
