using DashDetective.Services.Platform.Linux;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace DashDetective.Services.SystemMetrics;

/// <summary>
/// Per-disk activity on Linux, from <c>/proc/diskstats</c> — the counterpart to the Windows arm's PDH
/// <c>\PhysicalDisk(*)\*</c> counters, filling the same six fields so both platforms drive the Storage tab
/// and the Dashboard's disk cards identically.
///
/// <b>Only whole disks are reported.</b> <c>/proc/diskstats</c> lists <c>sda</c> and <c>sda1</c> alike, and
/// their I/O overlaps — counting both would roughly double every figure. <see cref="SysBlockFacts"/> says
/// which numbers are disks.
///
/// Every reading but the queue depth is a delta over the wall-clock interval since the previous call, so
/// this is <b>stateful</b>. The counters are monotonic but reset when a device is re-plugged, so a negative
/// delta is read as no activity — the same defence the Linux CPU samplers take.
/// </summary>
internal sealed class LinuxPhysicalDiskThroughputSampler : IPhysicalDiskThroughputSampler {
    // Concatenated forward-slash literal, never Path.Combine — see IProcFileSystem.
    private const string DiskstatsPath = "/proc/diskstats";

    private const double MillisecondsPerSecond = 1000.0;

    private readonly IProcFileSystem _proc;
    private readonly Func<double> _elapsedSeconds;

    private IReadOnlyDictionary<int, DiskStatsCounters> _previous;
    private double _previousSeconds;

    public LinuxPhysicalDiskThroughputSampler() : this(new ProcFileSystem(), StartClock()) { }

    /// <summary>Test seam: injects the filesystem and the clock so the rate arithmetic can be exercised
    /// against canned fixtures and a known interval from any dev machine.</summary>
    internal LinuxPhysicalDiskThroughputSampler(IProcFileSystem proc, Func<double> elapsedSeconds) {
        _proc = proc;
        _elapsedSeconds = elapsedSeconds;

        // Seed so the first Sample() reflects a real interval rather than everything since boot.
        _previous = ProcDiskstatsParser.Parse(_proc.ReadAllLines(DiskstatsPath));
        _previousSeconds = _elapsedSeconds();
    }

    /// <summary>Returns one reading per physical disk for the interval since the previous call. Empty when
    /// <c>/proc/diskstats</c> is unreadable, or when no measurable time has passed.</summary>
    public IReadOnlyList<DiskThroughputSample> Sample() {
        var current = ProcDiskstatsParser.Parse(_proc.ReadAllLines(DiskstatsPath));
        if (current.Count == 0)
            return Array.Empty<DiskThroughputSample>();

        var now = _elapsedSeconds();
        var interval = now - _previousSeconds;
        _previousSeconds = now;

        var previous = _previous;
        _previous = current;

        if (interval <= 0)
            return Array.Empty<DiskThroughputSample>();

        var disks = SysBlockFacts.DiskNumbers(_proc);
        var samples = new List<DiskThroughputSample>(disks.Count);

        foreach (var (diskNumber, counters) in current) {
            if (!disks.Contains(diskNumber))
                continue;

            // A disk that has only just appeared has no baseline, so it reports nothing for this tick
            // rather than its whole history since boot; the next tick measures it normally.
            if (!previous.TryGetValue(diskNumber, out var then))
                continue;

            samples.Add(Measure(diskNumber, then, counters, interval));
        }

        return samples;
    }

    /// <summary>Nothing to release — the seam is <c>IDisposable</c> for the PDH arm's sake.</summary>
    public void Dispose() { }

    /// <summary>Turns one disk's counter pair into the tab's six figures.</summary>
    private static DiskThroughputSample Measure(
        int diskNumber, DiskStatsCounters then, DiskStatsCounters now, double interval) {

        var reads = Delta(then.ReadsCompleted, now.ReadsCompleted);
        var writes = Delta(then.WritesCompleted, now.WritesCompleted);
        var serviceMs = Delta(then.MillisecondsReading, now.MillisecondsReading)
                      + Delta(then.MillisecondsWriting, now.MillisecondsWriting);

        // io_ticks counts milliseconds with at least one request outstanding — the direct analogue of
        // Windows' 100 − % Idle Time, and what every headline number and sparkline on the page renders.
        var busyPercent = Delta(then.IoMilliseconds, now.IoMilliseconds)
                        / (interval * MillisecondsPerSecond) * 100;

        // Mean service time over the transfers that actually completed; no transfers means no figure to
        // report rather than a zero that would read as "instant".
        var responseSeconds =
            reads + writes > 0 ? serviceMs / (reads + writes) / MillisecondsPerSecond : 0;

        var readBytes = Delta(then.SectorsRead, now.SectorsRead) * ProcDiskstatsParser.SectorBytes;
        var writtenBytes = Delta(then.SectorsWritten, now.SectorsWritten) * ProcDiskstatsParser.SectorBytes;

        return new DiskThroughputSample(
            diskNumber,
            readBytes / interval,
            writtenBytes / interval,
            Math.Clamp(busyPercent, 0, 100),
            responseSeconds,
            now.InFlight);
    }

    /// <summary>The rise in a monotonic counter, or 0 when it went backwards — which means the device was
    /// re-plugged and its counters restarted, not that it did negative work.</summary>
    private static double Delta(ulong then, ulong now) => now > then ? now - then : 0;

    /// <summary>A monotonic elapsed-seconds clock. The caller's timer is not exact, so rates are measured
    /// against real elapsed time — the same thing <c>NetworkUsageSampler</c> does.</summary>
    private static Func<double> StartClock() {
        var clock = Stopwatch.StartNew();
        return () => clock.Elapsed.TotalSeconds;
    }
}
