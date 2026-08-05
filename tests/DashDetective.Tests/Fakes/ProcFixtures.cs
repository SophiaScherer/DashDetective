using System.Globalization;
using System.Linq;

namespace DashDetective.Tests.Fakes;

/// <summary>
/// Canned <c>/proc</c> and <c>/sys</c> file bodies shared by the Linux provider tests, as plain C# raw
/// string literals — no embedded resources and no new build actions, matching the codebase's
/// zero-dependency ethos. A fixture used by exactly one test stays inline in that test.
/// </summary>
internal static class ProcFixtures {
    /// <summary>
    /// A modern <c>/proc/stat</c>: the aggregate plus four cores, all ten columns, and the non-<c>cpu</c>
    /// trailer that a parser has to skip. Every line totals to 17% busy — 10000 jiffies across
    /// <c>user…steal</c> of which 8300 are <c>idle + iowait</c> — so a diff against a doubled snapshot
    /// lands on a round number.
    /// </summary>
    public const string ProcStat =
        """
        cpu  1000 100 500 8000 300 0 100 0 0 0
        cpu0 250 25 125 2000 75 0 25 0 0 0
        cpu1 250 25 125 2000 75 0 25 0 0 0
        cpu2 250 25 125 2000 75 0 25 0 0 0
        cpu3 250 25 125 2000 75 0 25 0 0 0
        intr 45678901 22 1234 0 0 0 0 0 0 1
        ctxt 98765432
        btime 1717171717
        processes 123456
        procs_running 2
        procs_blocked 0
        softirq 12345678 1 234567 89 12345 0 0 6789 123456 0 987654
        """;

    /// <summary>The pre-2.6.11 seven-column form (no <c>steal</c>, <c>guest</c> or <c>guest_nice</c>) —
    /// what proves a parser reads by index with a length check rather than assuming ten.</summary>
    public const string ProcStatLegacy =
        """
        cpu  1000 100 500 8000 300 0 100
        cpu0 500 50 250 4000 150 0 50
        cpu1 500 50 250 4000 150 0 50
        """;

    /// <summary>One <c>/proc/stat</c> line — <c>StatLine("cpu0", 250, 25, …)</c>. Lets a test state the
    /// exact jiffy deltas it wants to assert on instead of counting columns in a literal.</summary>
    public static string StatLine(string cpu, params long[] fields) =>
        cpu + " " + string.Join(' ', fields.Select(f => f.ToString(CultureInfo.InvariantCulture)));

    /// <summary>Assembles a <c>/proc/stat</c> body from lines built with <see cref="StatLine"/>.</summary>
    public static string Stat(params string[] lines) => string.Join('\n', lines);
}
