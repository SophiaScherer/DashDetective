using DashDetective.Services.SystemMetrics;
using DashDetective.Tests.Fakes;
using Xunit;

namespace DashDetective.Tests.Services.SystemMetrics;

/// <summary>Covers <see cref="LinuxCpuSampler"/>: that it seeds from the aggregate line, reports the diff
/// between two reads rather than the time since boot, and degrades to 0 when <c>/proc/stat</c> is absent
/// or unusable.</summary>
public class LinuxCpuSamplerTests {
    private const string StatPath = "/proc/stat";

    [Fact]
    public void Sample_ReportsTheDiffBetweenReads() {
        // Seed at 17% busy since boot, then advance by 1000 total jiffies of which 750 are busy.
        var proc = new FakeProcFileSystem().WithFile(StatPath, ProcFixtures.ProcStat);
        var sampler = new LinuxCpuSampler(proc);

        proc.WithFile(StatPath, ProcFixtures.Stat(
            ProcFixtures.StatLine("cpu", 1750, 100, 500, 8250, 300, 0, 100, 0, 0, 0)));

        Assert.Equal(75.0, sampler.Sample());
    }

    /// <summary>The constructor seeds a snapshot, so the very first reading is an interval — not the
    /// whole-uptime average, which would read as a flat 17% here.</summary>
    [Fact]
    public void Sample_FirstCallAfterAnIdleInterval_ReturnsZero() {
        var proc = new FakeProcFileSystem().WithFile(StatPath, ProcFixtures.ProcStat);
        var sampler = new LinuxCpuSampler(proc);

        // 1000 jiffies pass, all of them idle.
        proc.WithFile(StatPath, ProcFixtures.Stat(
            ProcFixtures.StatLine("cpu", 1000, 100, 500, 9000, 300, 0, 100, 0, 0, 0)));

        Assert.Equal(0.0, sampler.Sample());
    }

    [Fact]
    public void Sample_SuccessiveIntervals_EachMeasureTheirOwn() {
        var proc = new FakeProcFileSystem().WithFile(StatPath,
            ProcFixtures.Stat(ProcFixtures.StatLine("cpu", 0, 0, 0, 0, 0, 0, 0, 0, 0, 0)));
        var sampler = new LinuxCpuSampler(proc);

        proc.WithFile(StatPath, ProcFixtures.Stat(
            ProcFixtures.StatLine("cpu", 250, 0, 0, 750, 0, 0, 0, 0, 0, 0)));
        Assert.Equal(25.0, sampler.Sample());

        // A second 1000-jiffy interval, this one 90% busy — the previous interval must not bleed in.
        proc.WithFile(StatPath, ProcFixtures.Stat(
            ProcFixtures.StatLine("cpu", 1150, 0, 0, 850, 0, 0, 0, 0, 0, 0)));
        Assert.Equal(90.0, sampler.Sample());
    }

    /// <summary>The aggregate roll-up is the first line; a sampler that took whatever line parsed first
    /// would still pass the tests above, so pin that it skips per-core lines when the order is reversed.</summary>
    [Fact]
    public void Sample_ReadsTheAggregateLineNotAPerCoreOne() {
        var proc = new FakeProcFileSystem().WithFile(StatPath, ProcFixtures.Stat(
            ProcFixtures.StatLine("cpu0", 0, 0, 0, 0, 0, 0, 0, 0, 0, 0),
            ProcFixtures.StatLine("cpu", 0, 0, 0, 0, 0, 0, 0, 0, 0, 0)));
        var sampler = new LinuxCpuSampler(proc);

        // The core goes flat out; the aggregate is half busy. Reading the core would report 100.
        proc.WithFile(StatPath, ProcFixtures.Stat(
            ProcFixtures.StatLine("cpu0", 1000, 0, 0, 0, 0, 0, 0, 0, 0, 0),
            ProcFixtures.StatLine("cpu", 500, 0, 0, 500, 0, 0, 0, 0, 0, 0)));

        Assert.Equal(50.0, sampler.Sample());
    }

    [Fact]
    public void Sample_LegacySevenColumnStat_StillReads() {
        var proc = new FakeProcFileSystem().WithFile(StatPath, ProcFixtures.ProcStatLegacy);
        var sampler = new LinuxCpuSampler(proc);

        proc.WithFile(StatPath, ProcFixtures.Stat(
            ProcFixtures.StatLine("cpu", 1400, 100, 500, 8600, 300, 0, 100)));

        Assert.Equal(40.0, sampler.Sample());
    }

    [Fact]
    public void Sample_MissingStat_ReturnsZeroForever() {
        var sampler = new LinuxCpuSampler(new FakeProcFileSystem());

        Assert.Equal(0.0, sampler.Sample());
        Assert.Equal(0.0, sampler.Sample());
    }

    [Fact]
    public void Sample_MalformedStat_ReturnsZero() {
        var proc = new FakeProcFileSystem().WithFile(StatPath, "not a stat file\nintr 1 2 3");

        Assert.Equal(0.0, new LinuxCpuSampler(proc).Sample());
    }

    /// <summary>A stat file that has not advanced between reads (two samples inside one jiffy) has no
    /// interval to divide by.</summary>
    [Fact]
    public void Sample_UnchangedStat_ReturnsZero() {
        var proc = new FakeProcFileSystem().WithFile(StatPath, ProcFixtures.ProcStat);

        Assert.Equal(0.0, new LinuxCpuSampler(proc).Sample());
    }

    [Fact]
    public void Sample_ReadsProcStatByLiteralForwardSlashPath() {
        var proc = new FakeProcFileSystem().WithFile(StatPath, ProcFixtures.ProcStat);

        new LinuxCpuSampler(proc).Sample();

        Assert.All(proc.Reads, path => Assert.Equal("/proc/stat", path));
        Assert.NotEmpty(proc.Reads);
    }
}
