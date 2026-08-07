using DashDetective.Services.Platform.Linux;
using System;
using System.Globalization;

namespace DashDetective.Services.SystemMetrics;

/// <summary>
/// The system-wide counters on Linux, assembled from three rootless sources: the file-cache size from
/// <c>/proc/meminfo</c>, the thread total from <c>/proc/loadavg</c>, and the process total from the
/// numeric entries under <c>/proc</c>.
///
/// <b>Handles have no Linux analogue and permanently read "—".</b> This is not a TODO: a Windows handle
/// covers events, threads, registry keys and more besides files, so the nearest candidate
/// (<c>/proc/sys/fs/file-nr</c>, open file descriptors) would put a number that means something different
/// under the same label. Reporting nothing is the honest answer.
///
/// Stateless and never throws — each field degrades to <c>null</c> on its own, and a wholly unreadable
/// <c>/proc</c> yields <c>null</c>.
/// </summary>
internal sealed class LinuxSystemPerformanceProvider : ISystemPerformanceProvider {
    // Concatenated forward-slash literals, never Path.Combine — see IProcFileSystem.
    private const string MeminfoPath = "/proc/meminfo";
    private const string LoadavgPath = "/proc/loadavg";
    private const string ProcRoot = "/proc";

    private readonly IProcFileSystem _proc;

    public LinuxSystemPerformanceProvider() : this(new ProcFileSystem()) { }

    /// <summary>Test seam: injects the filesystem so each source — and each source's absence — can be
    /// exercised against canned fixtures from any dev machine.</summary>
    internal LinuxSystemPerformanceProvider(IProcFileSystem proc) => _proc = proc;

    /// <summary>Returns the current counters, or <c>null</c> when no source answered at all.</summary>
    public SystemPerformanceSample? Read() {
        var cached = ReadCachedBytes();
        var threads = ReadThreadCount();
        var processes = ReadProcessCount();

        if (cached is null && threads is null && processes is null)
            return null;

        return new SystemPerformanceSample(cached, processes, threads, HandleCount: null);
    }

    /// <summary>Reclaimable page-cache bytes: <c>Cached</c> plus <c>SReclaimable</c>, the slab the kernel
    /// can hand back under pressure — together they are what <c>free -h</c> counts as buff/cache.</summary>
    private ulong? ReadCachedBytes() {
        var fields = ProcMeminfoParser.Parse(_proc.ReadAllLines(MeminfoPath));
        if (fields.Count == 0)
            return null;

        var cached = ProcMeminfoParser.Value(fields, "Cached");
        var reclaimable = ProcMeminfoParser.Value(fields, "SReclaimable");
        if (cached == 0 && reclaimable == 0)
            return null;

        // Saturate rather than wrap, matching the parser's own overflow contract.
        return cached <= ulong.MaxValue - reclaimable ? cached + reclaimable : ulong.MaxValue;
    }

    /// <summary>The kernel's total task count, from <c>/proc/loadavg</c>'s fourth field
    /// (<c>nr_running/nr_threads</c>). The denominator is <b>threads</b>, not processes — reading it as a
    /// process count is the mistake this comment exists to prevent.</summary>
    private int? ReadThreadCount() {
        var text = _proc.ReadAllText(LoadavgPath);
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var tokens = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length < 4)
            return null;

        var slash = tokens[3].IndexOf('/');
        if (slash < 0)
            return null;

        return int.TryParse(
            tokens[3].AsSpan(slash + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var threads)
            ? threads
            : null;
    }

    /// <summary>The number of live processes, counted from <c>/proc</c>'s numeric entries. One directory
    /// listing per read and no per-PID file opens, so it stays affordable at the sampling cadence — the
    /// full walk belongs to the Processes tab. A count of zero means the listing failed, not that the
    /// machine is idle, so it reports nothing.</summary>
    private int? ReadProcessCount() {
        var processes = 0;
        foreach (var entry in _proc.ListDirectory(ProcRoot)) {
            // Only all-digit entries are PIDs; /proc is full of named files and "self"/"thread-self" links.
            if (entry.Length > 0 && IsAllDigits(entry))
                processes++;
        }

        return processes > 0 ? processes : null;
    }

    private static bool IsAllDigits(string value) {
        foreach (var character in value) {
            if (!char.IsAsciiDigit(character))
                return false;
        }

        return true;
    }
}
