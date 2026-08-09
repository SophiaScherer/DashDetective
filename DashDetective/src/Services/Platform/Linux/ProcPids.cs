using System.Collections.Generic;
using System.Globalization;

namespace DashDetective.Services.Platform.Linux;

/// <summary>
/// The live PIDs, from <c>/proc</c>'s all-digit entries. A shared derivation rather than a parser — the
/// Performance tab's process count and the Processes tab's full walk both start from this listing, so
/// sharing it is what stops them disagreeing about which entries are processes (<c>/proc</c> is full of
/// named files, plus the <c>self</c> and <c>thread-self</c> links).
/// </summary>
internal static class ProcPids {
    // Concatenated forward-slash literals, never Path.Combine — see IProcFileSystem.
    private const string ProcRoot = "/proc";

    /// <summary>Every numeric entry under <c>/proc</c>, in listing order. Empty means the listing failed,
    /// not that the machine is idle — a host with no processes is impossible.</summary>
    internal static IReadOnlyList<int> List(IProcFileSystem proc) {
        var pids = new List<int>();
        foreach (var entry in proc.ListDirectory(ProcRoot)) {
            if (TryParsePid(entry, out var pid))
                pids.Add(pid);
        }

        return pids;
    }

    /// <summary>A directory name as a PID. Only all-digit names are PIDs, and the digit check runs first so
    /// <c>int.TryParse</c> never accepts a signed or whitespace-padded name that <c>/proc</c> cannot hold.</summary>
    private static bool TryParsePid(string entry, out int pid) {
        pid = 0;
        if (entry.Length == 0)
            return false;

        foreach (var character in entry) {
            if (!char.IsAsciiDigit(character))
                return false;
        }

        return int.TryParse(entry, NumberStyles.None, CultureInfo.InvariantCulture, out pid);
    }
}
