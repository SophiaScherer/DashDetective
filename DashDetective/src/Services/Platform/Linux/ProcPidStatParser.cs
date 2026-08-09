using System;
using System.Globalization;

namespace DashDetective.Services.Platform.Linux;

/// <summary>
/// Parses one process's <c>/proc/[pid]/stat</c>. Distinct from <see cref="ProcStatParser"/>, which reads the
/// machine-wide <c>/proc/stat</c> — the <c>Pid</c> in the name is the whole difference.
///
/// This one file carries most of what a process row needs: its short name, run state, parent, CPU time and
/// thread count. Reading them here rather than from <c>/proc/[pid]/status</c> and <c>/proc/[pid]/comm</c>
/// saves two file opens per process on every poll.
/// </summary>
internal static class ProcPidStatParser {
    // Indices within the tokens AFTER comm's closing parenthesis, so the kernel's field 3 (state) is 0.
    private const int StateIndex = 0;
    private const int ParentPidIndex = 1;
    private const int UtimeIndex = 11;
    private const int StimeIndex = 12;
    private const int ThreadsIndex = 17;

    // Every field read here sits at or before num_threads, so a shorter line is a torn read, not an old
    // kernel — the fields this parser wants have been in the same places since 2.6.
    private const int MinimumTokens = ThreadsIndex + 1;

    /// <summary>Parses the file body. Returns false for anything unreadable or truncated, which the caller
    /// treats as "this process vanished mid-walk" and skips.</summary>
    internal static bool TryParse(string? body, out ProcPidStat stat) {
        stat = default;
        if (string.IsNullOrEmpty(body))
            return false;

        // comm is parenthesised and may itself contain spaces and parentheses — "(Web Content)", "(a (b) c)"
        // — so the split point is the LAST ')'. Splitting the whole line on spaces produces a garbage parent
        // PID for exactly the processes a user cares about.
        var open = body.IndexOf('(');
        var close = body.LastIndexOf(')');
        if (open < 0 || close < open)
            return false;

        var tokens = body[(close + 1)..]
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length < MinimumTokens || tokens[StateIndex].Length != 1)
            return false;

        if (!TryParseInt(tokens[ParentPidIndex], out var parentPid)
            || !TryParseTicks(tokens[UtimeIndex], out var utime)
            || !TryParseTicks(tokens[StimeIndex], out var stime)
            || !TryParseInt(tokens[ThreadsIndex], out var threads))
            return false;

        stat = new ProcPidStat(body[(open + 1)..close], tokens[StateIndex][0], parentPid, utime + stime, threads);
        return true;
    }

    private static bool TryParseInt(string token, out int value) =>
        int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out value);

    private static bool TryParseTicks(string token, out ulong value) =>
        ulong.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out value);
}

/// <summary>
/// The fields <see cref="ProcPidStatParser"/> reads. <paramref name="Comm"/> is the kernel's short name,
/// <b>truncated at 15 characters</b>, so it is a fallback for the <c>cmdline</c> basename rather than the
/// name to show. <paramref name="CpuTicks"/> is <c>utime + stime</c> — the two are only ever wanted
/// together, and summing here keeps the USER_HZ conversion in one place.
/// </summary>
internal readonly record struct ProcPidStat(
    string Comm,
    char State,
    int ParentPid,
    ulong CpuTicks,
    int ThreadCount);
