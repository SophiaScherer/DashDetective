using System;
using System.Collections.Generic;
using System.Globalization;

namespace DashDetective.Services.Platform.Linux;

/// <summary>
/// Parses the two fields of <c>/proc/[pid]/status</c> that <see cref="ProcPidStatParser"/> cannot supply:
/// the owning user and the resident set size. Parent PID, thread count and run state are <b>deliberately
/// not read here</b> even though this file carries them — <c>stat</c> is already open for every process,
/// and a second source for the same number is a second answer.
///
/// Unlike <c>stat</c>, an unreadable <c>status</c> does not disqualify a process: the row still lists, with
/// an unknown owner and no memory figure. That is why this returns a value rather than a success flag.
/// </summary>
internal static class ProcPidStatusParser {
    private const long BytesPerKibibyte = 1024;

    /// <summary>Reads <c>Uid</c> and <c>VmRSS</c>, stopping as soon as both are found — this runs once per
    /// process per poll, and the fields sit in the first third of a ~60-line file.</summary>
    internal static ProcPidStatus Parse(IReadOnlyList<string> lines) {
        int? uid = null;
        long resident = 0;

        foreach (var line in lines) {
            var colon = line.IndexOf(':');
            if (colon <= 0)
                continue;

            var key = line.AsSpan(0, colon).Trim();
            var rest = line.AsSpan(colon + 1).Trim();
            if (rest.IsEmpty)
                continue;

            if (uid is null && key.Equals("Uid", StringComparison.Ordinal))
                uid = ParseRealUid(rest);
            else if (resident == 0 && key.Equals("VmRSS", StringComparison.Ordinal))
                resident = ParseKilobytes(rest);

            if (uid is not null && resident != 0)
                break;
        }

        return new ProcPidStatus(uid, resident);
    }

    /// <summary>The <b>first</b> of the line's four values. The kernel writes real, effective, saved-set and
    /// filesystem uids on one line; the real uid is the process's owner.</summary>
    private static int? ParseRealUid(ReadOnlySpan<char> rest) {
        var end = 0;
        while (end < rest.Length && char.IsAsciiDigit(rest[end]))
            end++;

        return end > 0
            && int.TryParse(rest[..end], NumberStyles.None, CultureInfo.InvariantCulture, out var uid)
            ? uid
            : null;
    }

    /// <summary>A <c>"345678 kB"</c> value in bytes. The <c>kB</c> label is kibibytes, as everywhere else in
    /// <c>/proc</c>; 0 for anything unparseable.</summary>
    private static long ParseKilobytes(ReadOnlySpan<char> rest) {
        var space = rest.IndexOf(' ');
        var number = space < 0 ? rest : rest[..space];

        if (!long.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out var kilobytes))
            return 0;

        // Saturate rather than wrap: a nonsensical value must not read as a tiny one.
        return kilobytes <= long.MaxValue / BytesPerKibibyte ? kilobytes * BytesPerKibibyte : long.MaxValue;
    }
}

/// <summary>
/// What <see cref="ProcPidStatusParser"/> reads. <paramref name="Uid"/> is <c>null</c> when unknown rather
/// than 0, because 0 is root — a missing read must never classify a user process as system.
/// <paramref name="ResidentBytes"/> is 0 when the file carries no <c>VmRSS</c>, which is the honest answer
/// for a kernel thread: it has no address space at all.
/// </summary>
internal readonly record struct ProcPidStatus(int? Uid, long ResidentBytes) {
    /// <summary>An unreadable or denied <c>status</c>.</summary>
    internal static ProcPidStatus None { get; } = new(null, 0);
}
