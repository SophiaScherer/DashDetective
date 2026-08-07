using DashDetective.Services.SystemMetrics;
using DashDetective.Tests.Fakes;
using System;
using Xunit;

namespace DashDetective.Tests.Services.SystemMetrics;

/// <summary>Covers <see cref="LinuxMemoryUsageSampler"/>: the used = total − available derivation, the
/// pre-3.14 fallback, and the overcommit case that must not be clamped. Also pins
/// <see cref="IMemoryUsageSampler.ForCurrentPlatform"/>'s dispatch.</summary>
public class LinuxMemoryUsageSamplerTests {
    private const string MeminfoPath = "/proc/meminfo";
    private const ulong Gib = 1024UL * 1024 * 1024;

    private static LinuxMemoryUsageSampler Over(string meminfo) =>
        new(new FakeProcFileSystem().WithFile(MeminfoPath, meminfo));

    /// <summary>The fixture is 16 GiB with 8 GiB available, so used is exactly half.</summary>
    [Fact]
    public void Sample_ReportsUsedAsTotalMinusAvailable() {
        var sample = Over(ProcFixtures.ProcMeminfo).Sample();

        Assert.Equal(16 * Gib, sample.TotalBytes);
        Assert.Equal(8 * Gib, sample.UsedBytes);
        Assert.Equal(50.0, sample.LoadPercent);
    }

    /// <summary>Commit is reported as the kernel's own pair, in bytes.</summary>
    [Fact]
    public void Sample_ReportsTheCommitPair() {
        var sample = Over(ProcFixtures.ProcMeminfo).Sample();

        Assert.Equal(9 * Gib, sample.CommittedBytes);
        Assert.Equal(10 * Gib, sample.CommitLimitBytes);
    }

    /// <summary>Under overcommit the charge legitimately exceeds the limit, so neither figure is clamped to
    /// the other — a clamp would hide exactly the condition the tile exists to show.</summary>
    [Fact]
    public void Sample_CommittedAboveTheLimit_IsNotClamped() {
        var sample = Over(
            """
            MemTotal:       16777216 kB
            MemAvailable:    8388608 kB
            CommitLimit:     8388608 kB
            Committed_AS:   12582912 kB
            """).Sample();

        Assert.Equal(12 * Gib, sample.CommittedBytes);
        Assert.Equal(8 * Gib, sample.CommitLimitBytes);
    }

    /// <summary>Pre-3.14 kernels have no <c>MemAvailable</c>. Falling back to free + cached + buffers is
    /// coarser than the kernel's estimate but far better than reporting the machine as fully used.</summary>
    [Fact]
    public void Sample_NoMemAvailable_FallsBackToFreePlusCachedPlusBuffers() {
        var sample = Over(
            """
            MemTotal:       16777216 kB
            MemFree:         2097152 kB
            Buffers:          524288 kB
            Cached:          5242880 kB
            """).Sample();

        // 2 + 5 + 0.5 = 7.5 GiB available, so 8.5 GiB used.
        Assert.Equal(8 * Gib + Gib / 2, sample.UsedBytes);
        Assert.Equal(53.125, sample.LoadPercent);
    }

    /// <summary>Available above total (a torn read across two lines) must floor at zero used rather than
    /// underflowing into an enormous figure.</summary>
    [Fact]
    public void Sample_AvailableAboveTotal_ReportsNothingUsed() {
        var sample = Over(
            """
            MemTotal:        1048576 kB
            MemAvailable:    2097152 kB
            """).Sample();

        Assert.Equal(0UL, sample.UsedBytes);
        Assert.Equal(0.0, sample.LoadPercent);
    }

    [Fact]
    public void Sample_NoMeminfo_ReturnsAZeroedReading() {
        var sampler = new LinuxMemoryUsageSampler(new FakeProcFileSystem());

        Assert.Equal(new MemorySample(0, 0, 0, 0, 0), sampler.Sample());
    }

    /// <summary>A file present but without <c>MemTotal</c> is as unusable as no file at all — a percentage
    /// needs a denominator.</summary>
    [Fact]
    public void Sample_NoMemTotal_ReturnsAZeroedReading() {
        Assert.Equal(new MemorySample(0, 0, 0, 0, 0), Over("MemFree:         2097152 kB").Sample());
    }

    /// <summary>Stateless, unlike the <c>/proc/stat</c> samplers — memory is an absolute reading, so two
    /// calls over the same fixture agree.</summary>
    [Fact]
    public void Sample_IsStateless() {
        var sampler = Over(ProcFixtures.ProcMeminfo);

        Assert.Equal(sampler.Sample(), sampler.Sample());
    }

    [Fact]
    public void Sample_ReadsMeminfoAndNothingElse() {
        var proc = new FakeProcFileSystem().WithFile(MeminfoPath, ProcFixtures.ProcMeminfo);

        _ = new LinuxMemoryUsageSampler(proc).Sample();

        Assert.Equal([MeminfoPath], proc.Reads);
    }

    [Fact]
    public void ForCurrentPlatform_PicksThisPlatformsReader() {
        var sampler = IMemoryUsageSampler.ForCurrentPlatform();

        if (OperatingSystem.IsWindows())
            Assert.IsType<WindowsMemoryUsageSampler>(sampler);
        else if (OperatingSystem.IsLinux())
            Assert.IsType<LinuxMemoryUsageSampler>(sampler);
        else
            Assert.IsType<UnsupportedMemoryUsageSampler>(sampler);
    }
}
