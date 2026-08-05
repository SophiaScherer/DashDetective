using DashDetective.Services.Platform.Linux;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace DashDetective.Services.SystemMetrics;

/// <summary>
/// Per-logical-processor utilisation on Linux, from the <c>cpu0</c>… lines of <c>/proc/stat</c> — the
/// per-core form of what <see cref="LinuxCpuSampler"/> reads from the aggregate line. <c>/proc/stat</c>
/// lists <b>online</b> CPUs only, so a hot-unplugged core simply stops being reported, which is the
/// contract the Performance tab's chart grid already expects.
///
/// Stateful (it holds the previous per-core snapshot) and never throws: an absent or malformed
/// <c>/proc/stat</c> yields an empty set.
/// </summary>
internal sealed class LinuxLogicalProcessorSampler : ILogicalProcessorSampler {
    // Concatenated forward-slash literal, never Path.Combine — see IProcFileSystem.
    private const string StatPath = "/proc/stat";
    private const string CpuPrefix = "cpu";

    private readonly IProcFileSystem _proc;
    private Dictionary<int, CoreCounters> _previous;

    public LinuxLogicalProcessorSampler() : this(new ProcFileSystem()) { }

    /// <summary>Test seam: injects the filesystem so the per-core diff can be exercised against canned
    /// fixtures from any dev machine.</summary>
    internal LinuxLogicalProcessorSampler(IProcFileSystem proc) {
        _proc = proc;
        // Seed so the first Sample() reflects a real interval rather than the time since boot.
        _previous = Read();
    }

    /// <summary>Returns one reading per online logical processor, ordered by core. Empty when
    /// <c>/proc/stat</c> is unreadable or reports no per-core lines.</summary>
    public IReadOnlyList<LogicalProcessorSample> Sample() {
        var current = Read();
        if (current.Count == 0)
            return Array.Empty<LogicalProcessorSample>();

        var samples = new List<LogicalProcessorSample>(current.Count);
        foreach (var (core, now) in current) {
            // A core that has just come online has no baseline to diff against, so it reports 0 for this
            // tick rather than its whole time since boot; the next tick measures it normally.
            double percent = 0;
            if (_previous.TryGetValue(core, out var then)) {
                var busyDelta = now.Busy > then.Busy ? now.Busy - then.Busy : 0;
                var totalDelta = now.Total > then.Total ? now.Total - then.Total : 0;
                percent = ProcStatParser.ComputeUsage(busyDelta, totalDelta);
            }

            samples.Add(new LogicalProcessorSample(
                CpuPrefix + core.ToString(CultureInfo.InvariantCulture),
                Group: 0, // Linux has no processor groups
                core,
                percent));
        }

        _previous = current;
        samples.Sort(static (a, b) => a.Core.CompareTo(b.Core));
        return samples;
    }

    /// <summary>Nothing to release — the seam is <c>IDisposable</c> for the PDH arm's sake.</summary>
    public void Dispose() { }

    /// <summary>Reads every <c>cpuN</c> line, keyed by core number. The aggregate <c>cpu</c> line has no
    /// digits after the prefix and is skipped.</summary>
    private Dictionary<int, CoreCounters> Read() {
        var counters = new Dictionary<int, CoreCounters>();
        foreach (var line in _proc.ReadAllLines(StatPath)) {
            if (!ProcStatParser.TryParseCpuLine(line, out var label, out var busy, out var total))
                continue;

            if (!int.TryParse(
                    label.AsSpan(CpuPrefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var core))
                continue;

            counters[core] = new CoreCounters(busy, total);
        }

        return counters;
    }

    /// <summary>One core's monotonic jiffy counters, as of one read.</summary>
    private readonly record struct CoreCounters(ulong Busy, ulong Total);
}
