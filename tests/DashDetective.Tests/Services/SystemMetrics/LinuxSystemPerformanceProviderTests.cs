using DashDetective.Services.SystemMetrics;
using DashDetective.Tests.Fakes;
using System;
using Xunit;

namespace DashDetective.Tests.Services.SystemMetrics;

/// <summary>Covers <see cref="LinuxSystemPerformanceProvider"/>: the three sources it assembles, the
/// per-field degradation when one is missing, and the handle count that is permanently absent by design.
/// Also pins <see cref="ISystemPerformanceProvider.ForCurrentPlatform"/>'s dispatch.</summary>
public class LinuxSystemPerformanceProviderTests {
    private const string MeminfoPath = "/proc/meminfo";
    private const string LoadavgPath = "/proc/loadavg";
    private const ulong Gib = 1024UL * 1024 * 1024;

    /// <summary>A fully-staged <c>/proc</c>: both files plus four PID directories and the named entries a
    /// real listing is full of.</summary>
    private static FakeProcFileSystem FullTree() =>
        new FakeProcFileSystem()
            .WithFile(MeminfoPath, ProcFixtures.ProcMeminfo)
            .WithFile(LoadavgPath, ProcFixtures.ProcLoadavg)
            .WithFile("/proc/1/stat", "1 (systemd) S 0")
            .WithFile("/proc/2/stat", "2 (kthreadd) S 0")
            .WithFile("/proc/412/stat", "412 (gnome-shell) S 1")
            .WithFile("/proc/1337/stat", "1337 (firefox) S 1")
            .WithFile("/proc/self/stat", "412 (gnome-shell) S 1")
            .WithFile("/proc/uptime", "1234.56 4321.00");

    /// <summary>Cached is the page cache plus the reclaimable slab — together what <c>free -h</c> counts as
    /// buff/cache. The fixture is 5 GiB + 0.75 GiB.</summary>
    [Fact]
    public void Read_ReportsCachedAsCachedPlusSReclaimable() {
        var sample = new LinuxSystemPerformanceProvider(FullTree()).Read();

        Assert.Equal(5 * Gib + Gib * 3 / 4, sample?.CachedBytes);
    }

    /// <summary>The fourth field of <c>/proc/loadavg</c> is <c>nr_running/nr_threads</c>: the denominator is
    /// the kernel's <b>thread</b> total. Reading it as a process count is the trap this fact guards.</summary>
    [Fact]
    public void Read_ReportsThreadsFromLoadavgsDenominator() {
        var sample = new LinuxSystemPerformanceProvider(FullTree()).Read();

        Assert.Equal(1234, sample?.ThreadCount);
    }

    /// <summary>Processes come from the numeric entries under <c>/proc</c>. The tree stages <c>self</c>,
    /// <c>meminfo</c>, <c>loadavg</c> and <c>uptime</c> alongside four PIDs, so a count of four proves the
    /// named entries are excluded.</summary>
    [Fact]
    public void Read_CountsOnlyNumericProcEntriesAsProcesses() {
        var sample = new LinuxSystemPerformanceProvider(FullTree()).Read();

        Assert.Equal(4, sample?.ProcessCount);
    }

    /// <summary>Permanent, not a TODO: a Windows handle covers far more than open files, so there is no
    /// figure that means the same thing under the same label.</summary>
    [Fact]
    public void Read_NeverReportsAHandleCount() {
        Assert.Null(new LinuxSystemPerformanceProvider(FullTree()).Read()?.HandleCount);
    }

    [Fact]
    public void Read_ReadsLoadavgForTheThreadCount() {
        var proc = FullTree();

        _ = new LinuxSystemPerformanceProvider(proc).Read();

        Assert.Contains(LoadavgPath, proc.Reads);
    }

    /// <summary>Each source degrades on its own: no <c>/proc/meminfo</c> blanks the cache figure and leaves
    /// the counts intact.</summary>
    [Fact]
    public void Read_NoMeminfo_ReportsTheCountsWithoutACacheFigure() {
        var proc = new FakeProcFileSystem()
            .WithFile(LoadavgPath, ProcFixtures.ProcLoadavg)
            .WithFile("/proc/1/stat", "1 (systemd) S 0");

        var sample = new LinuxSystemPerformanceProvider(proc).Read();

        Assert.Null(sample?.CachedBytes);
        Assert.Equal(1234, sample?.ThreadCount);
        Assert.Equal(1, sample?.ProcessCount);
    }

    /// <summary>And the reverse: no <c>/proc/loadavg</c> blanks only the thread count.</summary>
    [Fact]
    public void Read_NoLoadavg_ReportsEverythingElse() {
        var proc = new FakeProcFileSystem()
            .WithFile(MeminfoPath, ProcFixtures.ProcMeminfo)
            .WithFile("/proc/1/stat", "1 (systemd) S 0");

        var sample = new LinuxSystemPerformanceProvider(proc).Read();

        Assert.Null(sample?.ThreadCount);
        Assert.NotNull(sample?.CachedBytes);
        Assert.Equal(1, sample?.ProcessCount);
    }

    [Theory]
    [InlineData("0.52 0.58 0.59")]          // truncated before the task field
    [InlineData("0.52 0.58 0.59 2 56789")]  // no slash
    [InlineData("0.52 0.58 0.59 2/x 56789")]
    [InlineData("")]
    public void Read_MalformedLoadavg_ReportsNoThreadCount(string body) {
        var proc = FullTree().WithFile(LoadavgPath, body);

        Assert.Null(new LinuxSystemPerformanceProvider(proc).Read()?.ThreadCount);
    }

    /// <summary>A /proc with no PID directories means the listing failed, not that the machine is idle —
    /// zero processes is impossible, so it reports nothing.</summary>
    [Fact]
    public void Read_NoPidDirectories_ReportsNoProcessCount() {
        var proc = new FakeProcFileSystem()
            .WithFile(MeminfoPath, ProcFixtures.ProcMeminfo)
            .WithFile(LoadavgPath, ProcFixtures.ProcLoadavg);

        Assert.Null(new LinuxSystemPerformanceProvider(proc).Read()?.ProcessCount);
    }

    /// <summary>Nothing readable at all — a non-Linux host reached through the seam, or a container without
    /// <c>/proc</c> — is the whole-sample null, which every tile renders as "—".</summary>
    [Fact]
    public void Read_NothingReadable_ReturnsNull() {
        Assert.Null(new LinuxSystemPerformanceProvider(new FakeProcFileSystem()).Read());
    }

    /// <summary>Stateless: two reads over the same tree agree.</summary>
    [Fact]
    public void Read_IsStateless() {
        var provider = new LinuxSystemPerformanceProvider(FullTree());

        Assert.Equal(provider.Read(), provider.Read());
    }

    [Fact]
    public void ForCurrentPlatform_PicksThisPlatformsReader() {
        var provider = ISystemPerformanceProvider.ForCurrentPlatform();

        if (OperatingSystem.IsWindows())
            Assert.IsType<WindowsSystemPerformanceProvider>(provider);
        else if (OperatingSystem.IsLinux())
            Assert.IsType<LinuxSystemPerformanceProvider>(provider);
        else
            Assert.IsType<UnsupportedSystemPerformanceProvider>(provider);
    }

    /// <summary>The no-data arm honours the same contract, since it is what a platform whose milestone has
    /// not landed actually gets.</summary>
    [Fact]
    public void UnsupportedProvider_ReportsNothing() {
        Assert.Null(new UnsupportedSystemPerformanceProvider().Read());
    }
}
