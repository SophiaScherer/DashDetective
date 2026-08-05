using DashDetective.Services.SystemMetrics;
using DashDetective.Tests.Fakes;
using System;
using System.Linq;
using Xunit;

namespace DashDetective.Tests.Services.SystemMetrics;

/// <summary>Covers <see cref="LinuxLogicalProcessorSampler"/>: one reading per online core, each diffed
/// against its own previous snapshot, and the empty contract when <c>/proc/stat</c> is unusable. Also
/// pins <see cref="ILogicalProcessorSampler.ForCurrentPlatform"/>'s dispatch.</summary>
public class LinuxLogicalProcessorSamplerTests {
    private const string StatPath = "/proc/stat";

    /// <summary>Builds a stat body with the aggregate line plus one line per core, each core given its own
    /// busy/idle split so a cross-wired diff shows up immediately.</summary>
    private static string Stat(params (int Core, long Busy, long Idle)[] cores) =>
        ProcFixtures.Stat([
            ProcFixtures.StatLine("cpu", cores.Sum(c => c.Busy), 0, 0, cores.Sum(c => c.Idle), 0, 0, 0, 0, 0, 0),
            .. cores.Select(c => ProcFixtures.StatLine("cpu" + c.Core, c.Busy, 0, 0, c.Idle, 0, 0, 0, 0, 0, 0))]);

    [Fact]
    public void Sample_ReportsOneReadingPerCore_EachDiffedSeparately() {
        var proc = new FakeProcFileSystem().WithFile(StatPath, Stat((0, 0, 0), (1, 0, 0), (2, 0, 0)));
        var sampler = new LinuxLogicalProcessorSampler(proc);

        // Same 1000-jiffy interval on each core, at three different loads.
        proc.WithFile(StatPath, Stat((0, 250, 750), (1, 500, 500), (2, 1000, 0)));

        Assert.Equal([25.0, 50.0, 100.0], sampler.Sample().Select(s => s.Percent));
    }

    [Fact]
    public void Sample_LabelsEachCoreWithItsProcStatName() {
        var proc = new FakeProcFileSystem().WithFile(StatPath, Stat((0, 0, 0), (1, 0, 0)));
        var sampler = new LinuxLogicalProcessorSampler(proc);

        proc.WithFile(StatPath, Stat((0, 500, 500), (1, 500, 500)));
        var samples = sampler.Sample();

        Assert.Equal(["cpu0", "cpu1"], samples.Select(s => s.Instance));
        Assert.Equal([0, 1], samples.Select(s => s.Core));
        Assert.All(samples, s => Assert.Equal(0, s.Group)); // Linux has no processor groups
    }

    /// <summary>The aggregate roll-up is not a logical processor; including it would add a phantom chart
    /// and make the count wrong.</summary>
    [Fact]
    public void Sample_ExcludesTheAggregateCpuLine() {
        var proc = new FakeProcFileSystem().WithFile(StatPath, ProcFixtures.ProcStat);
        var sampler = new LinuxLogicalProcessorSampler(proc);

        proc.WithFile(StatPath, Stat((0, 1, 1), (1, 1, 1), (2, 1, 1), (3, 1, 1)));

        Assert.Equal(4, sampler.Sample().Count);
    }

    /// <summary>Cores are reported in numeric order, not the textual order a naive sort would give —
    /// "cpu10" must not land between "cpu1" and "cpu2", or the chart labels desynchronise.</summary>
    [Fact]
    public void Sample_OrdersCoresNumerically() {
        var cores = Enumerable.Range(0, 12).Select(i => (Core: i, Busy: 0L, Idle: 0L)).ToArray();
        var proc = new FakeProcFileSystem().WithFile(StatPath, Stat(cores));
        var sampler = new LinuxLogicalProcessorSampler(proc);

        proc.WithFile(StatPath, Stat([.. cores.Select(c => (c.Core, Busy: 1L, Idle: 1L))]));

        Assert.Equal(Enumerable.Range(0, 12), sampler.Sample().Select(s => s.Core));
    }

    /// <summary><c>/proc/stat</c> lists online CPUs only. A core that appears mid-run has no previous
    /// snapshot, and must report 0 for that tick rather than its whole time since boot.</summary>
    [Fact]
    public void Sample_CoreComingOnline_ReportsZeroUntilItHasAnInterval() {
        var proc = new FakeProcFileSystem().WithFile(StatPath, Stat((0, 0, 0)));
        var sampler = new LinuxLogicalProcessorSampler(proc);

        proc.WithFile(StatPath, Stat((0, 500, 500), (1, 900, 100)));
        var first = sampler.Sample();

        Assert.Equal([50.0, 0.0], first.Select(s => s.Percent));

        // Now cpu1 has a baseline, so its next interval is measured normally.
        proc.WithFile(StatPath, Stat((0, 1000, 1000), (1, 1650, 350)));
        Assert.Equal([50.0, 75.0], sampler.Sample().Select(s => s.Percent));
    }

    /// <summary>A core going offline disappears from the file; the reading must simply shrink rather than
    /// keep reporting a stale value.</summary>
    [Fact]
    public void Sample_CoreGoingOffline_DropsOutOfTheReading() {
        var proc = new FakeProcFileSystem().WithFile(StatPath, Stat((0, 0, 0), (1, 0, 0)));
        var sampler = new LinuxLogicalProcessorSampler(proc);

        proc.WithFile(StatPath, Stat((0, 500, 500)));

        Assert.Equal(["cpu0"], sampler.Sample().Select(s => s.Instance));
    }

    [Fact]
    public void Sample_MissingStat_ReturnsEmptyForever() {
        var sampler = new LinuxLogicalProcessorSampler(new FakeProcFileSystem());

        Assert.Empty(sampler.Sample());
        Assert.Empty(sampler.Sample());
    }

    /// <summary>A uniprocessor VM still has a <c>cpu0</c> line; only the aggregate would leave nothing to
    /// chart.</summary>
    [Fact]
    public void Sample_AggregateLineOnly_ReturnsEmpty() {
        var proc = new FakeProcFileSystem().WithFile(StatPath,
            ProcFixtures.StatLine("cpu", 1000, 100, 500, 8000, 300, 0, 100, 0, 0, 0));

        Assert.Empty(new LinuxLogicalProcessorSampler(proc).Sample());
    }

    [Fact]
    public void ForCurrentPlatform_PicksThisPlatformsReader() {
        using var sampler = ILogicalProcessorSampler.ForCurrentPlatform();

        if (OperatingSystem.IsWindows())
            Assert.IsType<WindowsLogicalProcessorSampler>(sampler);
        else if (OperatingSystem.IsLinux())
            Assert.IsType<LinuxLogicalProcessorSampler>(sampler);
        else
            Assert.IsType<UnsupportedLogicalProcessorSampler>(sampler);
    }
}
