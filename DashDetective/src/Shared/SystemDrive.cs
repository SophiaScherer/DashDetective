using System;
using System.IO;

namespace DashDetective.Shared;

/// <summary>
/// The drive Windows itself is installed on. Centralises the
/// <c>Path.GetPathRoot(Environment.SystemDirectory)</c> idiom so labels don't hardcode "C:".
/// </summary>
public static class SystemDrive {
    /// <summary>The system drive's letter (e.g. 'C'), falling back to 'C' when it can't be resolved —
    /// the value can't change while the process runs, so it's resolved once.</summary>
    public static char Letter { get; } = ReadLetter();

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
}
