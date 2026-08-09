using DashDetective.Services.Platform.Linux;
using DashDetective.Services.SystemMetrics;
using DashDetective.Tests.Fakes;
using System.Collections.Generic;
using Xunit;

namespace DashDetective.Tests.Services.SystemMetrics;

/// <summary>Covers <see cref="LinuxPhysicalDiskThroughputSampler"/>: the rate arithmetic over a known
/// interval, the partition filter that stops a disk double-counting its own I/O, and the counter reset a
/// re-plugged device causes.</summary>
public class LinuxPhysicalDiskThroughputSamplerTests {
    private const string DiskstatsPath = "/proc/diskstats";
    private static readonly int Sda = (8 << 20) | 0;

    /// <summary>Advances one second per call, so a diff lands on the fixture's round numbers.</summary>
    private sealed class StepClock(double step = 1.0) {
        private double _now;

        public double Next() {
            var value = _now;
            _now += step;
            return value;
        }
    }

    /// <summary>A sampler primed on the fixture, ready for one diff against
    /// <see cref="ProcFixtures.ProcDiskstatsLater"/>.</summary>
    private static (LinuxPhysicalDiskThroughputSampler Sampler, FakeProcFileSystem Proc) Primed(
        string body = ProcFixtures.ProcDiskstats) {
        var proc = new FakeProcFileSystem()
            .WithVirtualBoxBlockTree()
            .WithFile(DiskstatsPath, body);
        var clock = new StepClock();

        return (new LinuxPhysicalDiskThroughputSampler(proc, clock.Next), proc);
    }

    private static DiskThroughputSample SampleAfter(string later) {
        var (sampler, proc) = Primed();
        proc.WithFile(DiskstatsPath, later);

        return Assert.Single(sampler.Sample());
    }

    /// <summary>+1024 sectors read and +2048 written over one second, at 512 bytes a sector.</summary>
    [Fact]
    public void Sample_ReportsThroughputOverTheElapsedInterval() {
        var sample = SampleAfter(ProcFixtures.ProcDiskstatsLater);

        Assert.Equal(1024 * 512, sample.ReadBytesPerSec);
        Assert.Equal(2048 * 512, sample.WriteBytesPerSec);
    }

    /// <summary>The field the source plan omitted, and the one every headline number and sparkline on the
    /// page renders: +250 ms of io_ticks in one second is 25% active.</summary>
    [Fact]
    public void Sample_ReportsActiveTimeFromIoTicks() =>
        Assert.Equal(25, SampleAfter(ProcFixtures.ProcDiskstatsLater).ActivePercent);

    /// <summary>+50 ms reading and +50 ms writing across 4 completed transfers is 25 ms each, reported in
    /// seconds like the Windows arm's PDH counter.</summary>
    [Fact]
    public void Sample_ReportsMeanResponseOverCompletedTransfers() =>
        Assert.Equal(0.025, SampleAfter(ProcFixtures.ProcDiskstatsLater).ResponseSeconds, 6);

    /// <summary>Queue depth is instantaneous, not a delta — diffing it would report 2 outstanding requests
    /// as a change of 2 rather than a depth of 2.</summary>
    [Fact]
    public void Sample_ReportsQueueDepthAsRead() =>
        Assert.Equal(2, SampleAfter(ProcFixtures.ProcDiskstatsLater).QueueLength);

    /// <summary>No completed transfers means no mean to report; a zero here would read as an instant
    /// response rather than as no data.</summary>
    [Fact]
    public void Sample_WithNoCompletedTransfers_ReportsNoResponseTime() =>
        Assert.Equal(0, SampleAfter("8 0 sda 5000 100 2048 800 3000 200 4096 900 0 1000 1700").ResponseSeconds);

    /// <summary>The fixture lists sda, sda1 and sda2. Reporting all three would roughly double every figure
    /// on the page, since a partition's I/O is also its disk's.</summary>
    [Fact]
    public void Sample_ReportsTheDiskOnlyAndNotItsPartitions() {
        var (sampler, proc) = Primed();
        proc.WithFile(DiskstatsPath, ProcFixtures.ProcDiskstats);

        var sample = Assert.Single(sampler.Sample());
        Assert.Equal(Sda, sample.DiskNumber);
    }

    /// <summary>Loop devices are filtered at the block reader, so their rows never become samples
    /// either.</summary>
    [Fact]
    public void Sample_IgnoresLoopDeviceRows() {
        var (sampler, proc) = Primed();
        proc.WithFile(DiskstatsPath, ProcFixtures.ProcDiskstats);

        Assert.DoesNotContain(sampler.Sample(), s => s.DiskNumber == ((7 << 20) | 3));
    }

    /// <summary>Counters restart when a device is re-plugged. Reading the drop as negative work would show
    /// a nonsensical rate; it means no measurable activity this tick.</summary>
    [Fact]
    public void Sample_WithCountersResetBackwards_ReportsNoActivity() {
        var sample = SampleAfter("8 0 sda 1 0 8 1 1 0 8 1 0 1 2");

        Assert.Equal(0, sample.ReadBytesPerSec);
        Assert.Equal(0, sample.WriteBytesPerSec);
        Assert.Equal(0, sample.ActivePercent);
    }

    /// <summary>The constructor primes a baseline, so the first call measures an interval rather than
    /// everything since boot — a disk idle since the last read reports zero, not its lifetime average.</summary>
    [Fact]
    public void Sample_WithNoChange_ReportsZero() {
        var sample = SampleAfter(ProcFixtures.ProcDiskstats);

        Assert.Equal(0, sample.ReadBytesPerSec);
        Assert.Equal(0, sample.ActivePercent);
    }

    /// <summary>A disk that appears mid-run has no baseline, so it waits a tick rather than reporting its
    /// whole history as one interval's worth.</summary>
    [Fact]
    public void Sample_ADiskAppearingMidRun_WaitsForABaseline() {
        var proc = new FakeProcFileSystem()
            .WithVirtualBoxBlockTree()
            .WithFile(DiskstatsPath, "8 0 sda 0 0 0 0 0 0 0 0 0 0 0");
        var sampler = new LinuxPhysicalDiskThroughputSampler(proc, new StepClock().Next);

        proc.WithFile("/sys/block/sdb/dev", "8:16\n")
            .WithFile("/sys/block/sdb/size", "1024\n")
            .WithFile(DiskstatsPath,
                "8 0 sda 0 0 0 0 0 0 0 0 0 0 0\n8 16 sdb 99 0 9999 99 99 0 9999 99 0 999 999");

        Assert.DoesNotContain(sampler.Sample(), s => s.DiskNumber == ((8 << 20) | 16));
    }

    /// <summary>An unreadable <c>/proc/diskstats</c> leaves the surfaces at their last value rather than
    /// failing the page.</summary>
    [Fact]
    public void Sample_WithNoDiskstats_ReportsNothing() {
        var sampler = new LinuxPhysicalDiskThroughputSampler(
            new FakeProcFileSystem(), new StepClock().Next);

        Assert.Empty(sampler.Sample());
    }

    /// <summary>The seam's contract: keyed by the same packed number the drive cards are, or the samples
    /// would never match a card.</summary>
    [Fact]
    public void Sample_KeysReadingsByTheSameNumberTheBlockReaderDerives() {
        var (sampler, proc) = Primed();
        proc.WithFile(DiskstatsPath, ProcFixtures.ProcDiskstatsLater);

        var expected = SysBlockFacts.Read(proc).Disks[0].DiskNumber;
        Assert.Equal(expected, Assert.Single(sampler.Sample()).DiskNumber);
    }

    /// <summary>Nothing to release, but the seam is disposable for the PDH arm's sake and this arm must
    /// tolerate it.</summary>
    [Fact]
    public void Dispose_IsSafeToCallTwice() {
        var (sampler, _) = Primed();

        sampler.Dispose();
        sampler.Dispose();
    }

    /// <summary>An interval the clock reports as zero would divide by zero; the sampler reports nothing for
    /// that tick instead.</summary>
    [Fact]
    public void Sample_WithNoElapsedTime_ReportsNothing() {
        var proc = new FakeProcFileSystem()
            .WithVirtualBoxBlockTree()
            .WithFile(DiskstatsPath, ProcFixtures.ProcDiskstats);
        var sampler = new LinuxPhysicalDiskThroughputSampler(proc, static () => 0);

        Assert.Empty((IEnumerable<DiskThroughputSample>)sampler.Sample());
    }
}
