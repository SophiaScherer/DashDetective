using DashDetective.Services.Platform.Linux;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace DashDetective.Tabs.Processes;

/// <summary>
/// Builds the process snapshot by walking <c>/proc</c>. Runs off the UI thread via <see cref="GetAsync"/>
/// and never throws, matching the Windows provider it stands opposite.
///
/// Five small files per process — <c>stat</c>, <c>status</c>, <c>cmdline</c>, <c>cgroup</c>, <c>io</c> — of
/// which only <c>stat</c> is required: a PID whose <c>stat</c> has gone is a process that exited mid-walk
/// and is skipped, while every other read degrades on its own. <c>/proc/[pid]</c> disappearing under the
/// reader is the normal case, not the exceptional one.
///
/// <b>Per-process GPU is reported as 0 always</b> — a permanent gap rather than a TODO, since Linux exposes
/// no rootless per-process GPU accounting at all.
///
/// CPU% and the disk rate are differences across consecutive snapshots, so the previous tick and byte
/// tables are held between calls and swapped each pass to evict exited PIDs. Not thread-safe and
/// SINGLE-CONSUMER, exactly as the Windows provider. Portable managed code over
/// <see cref="IProcFileSystem"/>, so it carries no <c>[SupportedOSPlatform]</c>; the platform check lives
/// in <see cref="IProcessSnapshotProvider.ForCurrentPlatform"/>.
/// </summary>
internal sealed class LinuxProcessSnapshotProvider : IProcessSnapshotProvider {
    // Concatenated forward-slash literals, never Path.Combine — see IProcFileSystem.
    private const string ProcRoot = "/proc/";

    /// <summary>Kernel tick rate for the <c>utime</c>/<c>stime</c> counters. There is no rootless way to
    /// read <c>sysconf(_SC_CLK_TCK)</c>, and 100 is universal on Linux regardless of <c>CONFIG_HZ</c>.</summary>
    private const double UserHz = 100;

    /// <summary>Logical processors — the divisor that puts CPU% on Task Manager's 0–100 whole-machine
    /// scale, the same one <c>WindowsProcessSnapshotProvider</c> uses.</summary>
    private static readonly int LogicalProcessors = Environment.ProcessorCount > 0 ? Environment.ProcessorCount : 1;

    private readonly IProcFileSystem _proc;
    private readonly Dictionary<int, ulong> _prevCpuTicks = new();
    private readonly Dictionary<int, ulong> _prevIoBytes = new();
    private DateTime _prevSampledAt;

    public LinuxProcessSnapshotProvider() : this(new ProcFileSystem()) { }

    /// <summary>Test seam: injects the filesystem so the whole walk runs against canned fixtures from any
    /// dev machine.</summary>
    internal LinuxProcessSnapshotProvider(IProcFileSystem proc) => _proc = proc;

    public Task<IReadOnlyList<ProcessInfo>> GetAsync() => Task.Run(Snapshot);

    private IReadOnlyList<ProcessInfo> Snapshot() {
        var now = DateTime.UtcNow;
        // First snapshot has no prior point, so every rate reads 0 this pass and real next pass.
        var wallSeconds = _prevSampledAt == default ? 0 : (now - _prevSampledAt).TotalSeconds;

        var pids = ProcPids.List(_proc);
        var result = new List<ProcessInfo>(pids.Count);
        var nextCpuTicks = new Dictionary<int, ulong>(pids.Count);
        var nextIoBytes = new Dictionary<int, ulong>(pids.Count);

        foreach (var pid in pids) {
            var root = ProcRoot + pid.ToString(CultureInfo.InvariantCulture) + "/";

            // stat is read first and gates the rest: no stat, no process, and no four wasted reads.
            if (!ProcPidStatParser.TryParse(_proc.ReadAllText(root + "stat"), out var stat))
                continue;

            nextCpuTicks[pid] = stat.CpuTicks;

            var status = ProcPidStatusParser.Parse(_proc.ReadAllLines(root + "status"));
            var cmdline = _proc.ReadAllText(root + "cmdline");
            var cgroup = ProcCgroupParser.Parse(_proc.ReadAllLines(root + "cgroup"));

            var category = LinuxProcessClassifier.Classify(
                pid, stat.ParentPid, stat.State, status.Uid, ProcPidName.HasCommandLine(cmdline), cgroup);

            result.Add(new ProcessInfo(
                pid,
                stat.ParentPid,
                NameFrom(cmdline, stat.Comm),
                StatusFor(stat.State),
                CpuPercentFor(pid, stat.CpuTicks, wallSeconds),
                status.ResidentBytes,
                stat.ThreadCount,
                category,
                DiskRateFor(pid, root, nextIoBytes, wallSeconds),
                GpuPercent: 0));
        }

        Swap(_prevCpuTicks, nextCpuTicks);
        Swap(_prevIoBytes, nextIoBytes);
        _prevSampledAt = now;

        return result;
    }

    private static void Swap(Dictionary<int, ulong> target, Dictionary<int, ulong> source) {
        target.Clear();
        foreach (var pair in source)
            target[pair.Key] = pair.Value;
    }

    /// <summary>Disk rate in bytes/sec over the interval, recording the current total for the next diff. A
    /// process you do not own denies <c>io</c> entirely, which reads as no rate rather than as an error.</summary>
    private double DiskRateFor(int pid, string root, Dictionary<int, ulong> nextIoBytes, double wallSeconds) {
        if (!ProcPidIoParser.TryParse(_proc.ReadAllLines(root + "io"), out var bytes))
            return 0;
        nextIoBytes[pid] = bytes;

        if (wallSeconds <= 0 || !_prevIoBytes.TryGetValue(pid, out var prev) || bytes < prev)
            return 0;
        return (bytes - prev) / wallSeconds;
    }

    /// <summary>CPU% for this interval. A counter that went backwards means the PID was reused, so the
    /// process starts fresh rather than reporting a nonsense spike.</summary>
    private double CpuPercentFor(int pid, ulong ticks, double wallSeconds) =>
        _prevCpuTicks.TryGetValue(pid, out var prev) && ticks >= prev
            ? ComputeCpuPercent(ticks - prev, wallSeconds, LogicalProcessors)
            : 0;

    /// <summary>Pure interval math, split out so it can be unit-tested with injected values — the same
    /// shape as <c>ProcStatParser.ComputeUsage</c>. Clamps defensively.</summary>
    internal static double ComputeCpuPercent(ulong deltaTicks, double wallSeconds, int logicalProcessors) {
        if (deltaTicks == 0 || wallSeconds <= 0 || logicalProcessors <= 0)
            return 0;

        var percent = deltaTicks / UserHz / (wallSeconds * logicalProcessors) * 100;
        return percent > 100 ? 100 : percent;
    }

    /// <summary>This tab's placeholder for a process that names itself nowhere. <see cref="ProcPidName"/>
    /// reports "" rather than substituting, because the Network tab's placeholder differs.</summary>
    internal static string NameFrom(string? cmdline, string comm) =>
        ProcPidName.From(cmdline, comm) is { Length: > 0 } name ? name : "Unknown";

    /// <summary>The run state as the column's text. Windows reports only "Running" or "Not responding", so
    /// the states that mean the same thing there collapse to "Running" — that string is also what tints the
    /// row's dot green in <c>ProcessRow</c>.</summary>
    internal static string StatusFor(char state) => state switch {
        'D' => "Waiting",              // uninterruptible sleep, almost always blocked on I/O
        'T' or 't' => "Suspended",     // job-control stopped, or stopped by a tracer
        'Z' => "Zombie",
        _ => "Running",                // R, S, I — and anything a later kernel adds
    };
}
