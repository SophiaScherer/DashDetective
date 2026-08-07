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

    /// <summary>
    /// A two-core <c>/proc/cpuinfo</c>. Built by joining escaped strings rather than as a raw literal
    /// because the real file separates key from value with <b>tabs</b>, and the repo's
    /// <c>indent_style = space</c> makes a literal tab inside a raw literal a formatting hazard. The tabs
    /// are the point: a parser that splits on a fixed layout instead of trimming around the colon passes
    /// a space-separated fixture and fails on a real machine.
    /// </summary>
    public static readonly string ProcCpuInfo = string.Join('\n', [
        "processor\t: 0",
        "vendor_id\t: GenuineIntel",
        "model name\t: Intel(R) Core(TM) i7-9700K CPU @ 3.60GHz",
        "cpu MHz\t\t: 3600.000",
        "cache size\t: 12288 KB",
        "",
        "processor\t: 1",
        "vendor_id\t: GenuineIntel",
        "model name\t: Intel(R) Core(TM) i7-9700K CPU @ 3.60GHz",
        "cpu MHz\t\t: 2400.000",
        "cache size\t: 12288 KB",
        ""]);

    /// <summary>
    /// A stock Ubuntu <c>/proc/meminfo</c>, trimmed to the fields the app reads plus enough neighbours to
    /// prove the parser skips what it does not know. 16 GiB total; the numbers are round in <b>kB</b> so a
    /// byte expectation is a visible ×1024. <c>HugePages_Total</c> is deliberately present: it is a count
    /// with no unit, which a parser that assumes every value is kB gets wrong.
    /// </summary>
    public const string ProcMeminfo =
        """
        MemTotal:       16777216 kB
        MemFree:         2097152 kB
        MemAvailable:    8388608 kB
        Buffers:          524288 kB
        Cached:          5242880 kB
        SwapCached:            0 kB
        Active:          6291456 kB
        Inactive:        4194304 kB
        SwapTotal:       2097152 kB
        SwapFree:        2097152 kB
        Dirty:              1024 kB
        Slab:            1048576 kB
        SReclaimable:     786432 kB
        SUnreclaim:       262144 kB
        CommitLimit:    10485760 kB
        Committed_AS:    9437184 kB
        HugePages_Total:       0
        Hugepagesize:       2048 kB
        """;

    /// <summary>A <c>/proc/loadavg</c>: three load averages, then <c>nr_running/nr_threads</c> — 2 runnable
    /// of <b>1234 threads</b>, not processes — then the last-used PID.</summary>
    public const string ProcLoadavg = "0.52 0.58 0.59 2/1234 56789\n";

    /// <summary>One <c>/proc/stat</c> line — <c>StatLine("cpu0", 250, 25, …)</c>. Lets a test state the
    /// exact jiffy deltas it wants to assert on instead of counting columns in a literal.</summary>
    public static string StatLine(string cpu, params long[] fields) =>
        cpu + " " + string.Join(' ', fields.Select(f => f.ToString(CultureInfo.InvariantCulture)));

    /// <summary>Assembles a <c>/proc/stat</c> body from lines built with <see cref="StatLine"/>.</summary>
    public static string Stat(params string[] lines) => string.Join('\n', lines);
}
