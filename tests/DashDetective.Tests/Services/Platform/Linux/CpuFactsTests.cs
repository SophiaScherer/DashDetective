using DashDetective.Services.Platform.Linux;
using DashDetective.Tests.Fakes;
using Xunit;

namespace DashDetective.Tests.Services.Platform.Linux;

/// <summary>Covers <see cref="CpuFacts"/>: the core-count derivation that has to be right on both a
/// hyperthreaded chip and a multi-socket board, the clock sources in their preference order — including
/// the one that is deliberately never consulted — and the suffixed cache size.</summary>
public class CpuFactsTests {
    private const string CpuInfoPath = "/proc/cpuinfo";
    private const string CpuRoot = "/sys/devices/system/cpu";

    private static FakeProcFileSystem WithCpuInfo(string body) =>
        new FakeProcFileSystem().WithFile(CpuInfoPath, body);

    [Fact]
    public void Read_TakesTheModelNameFromTheFirstBlock() =>
        Assert.Equal(
            "Intel(R) Core(TM) i7-9700K CPU @ 3.60GHz",
            CpuFacts.Read(WithCpuInfo(ProcFixtures.ProcCpuInfo)).Name);

    [Fact]
    public void Read_CountsOneLogicalCorePerBlock() =>
        Assert.Equal(8, CpuFacts.Read(WithCpuInfo(ProcFixtures.AmdCpuInfo)).LogicalCores);

    /// <summary>
    /// The fixture is two sockets of two cores, each core hyperthreaded — eight blocks describing four
    /// physical cores. The distinct <c>(physical id, core id)</c> pairs are the only reading that gets
    /// this right: counting blocks reports 8, and taking <c>core id</c> alone would merge socket 1's cores
    /// into socket 0's because both number their cores from zero.
    /// </summary>
    [Fact]
    public void Read_CountsPhysicalCoresFromDistinctPackageAndCorePairs() =>
        Assert.Equal(4, CpuFacts.Read(WithCpuInfo(ProcFixtures.AmdCpuInfo)).PhysicalCores);

    /// <summary>Some kernels write <c>cpu cores</c> without <c>core id</c>. Multiplying by the socket count
    /// recovers the total, which taking <c>cpu cores</c> alone would halve on a dual-socket board.</summary>
    [Fact]
    public void Read_WithNoCoreId_FallsBackToCpuCoresTimesSockets() {
        var facts = CpuFacts.Read(WithCpuInfo(
            "processor\t: 0\nphysical id\t: 0\ncpu cores\t: 6\n\n"
            + "processor\t: 1\nphysical id\t: 1\ncpu cores\t: 6\n"));

        Assert.Equal(12, facts.PhysicalCores);
    }

    /// <summary>ARM and many virtualised <c>cpuinfo</c>s carry none of the topology keys. Zero renders "—"
    /// rather than a guess — the logical count is still real and still reported.</summary>
    [Fact]
    public void Read_WithNoTopologyKeys_ReportsNoPhysicalCores() {
        var facts = CpuFacts.Read(WithCpuInfo("processor\t: 0\nCPU implementer\t: 0x41\n"));

        Assert.Equal(0, facts.PhysicalCores);
        Assert.Equal(1, facts.LogicalCores);
    }

    /// <summary>The rated clock's preferred source.</summary>
    [Fact]
    public void Read_PrefersCpuinfoMaxFreq() {
        var proc = WithCpuInfo(ProcFixtures.ProcCpuInfo)
            .WithFile(CpuRoot + "/cpu0/cpufreq/cpuinfo_max_freq", "4900000\n");

        Assert.Equal(4900, CpuFacts.Read(proc).MaxClockMhz);
    }

    /// <summary>Taking the highest across cores rather than <c>cpu0</c>'s: on a heterogeneous chip
    /// <c>cpu0</c> can be a little core, which would report the slow half of the machine as its rating.</summary>
    [Fact]
    public void Read_TakesTheHighestMaxFreqAcrossCores() {
        var proc = WithCpuInfo(ProcFixtures.ProcCpuInfo)
            .WithFile(CpuRoot + "/cpu0/cpufreq/cpuinfo_max_freq", "2000000\n")
            .WithFile(CpuRoot + "/cpu1/cpufreq/cpuinfo_max_freq", "3800000\n");

        Assert.Equal(3800, CpuFacts.Read(proc).MaxClockMhz);
    }

    /// <summary>cpufreq is usually absent under VirtualBox, so the clock in the model name is the only
    /// source a guest has. It is the rated base clock, which is what this field means.</summary>
    [Fact]
    public void Read_WithNoCpufreq_FallsBackToTheClockInTheModelName() =>
        Assert.Equal(3600, CpuFacts.Read(WithCpuInfo(ProcFixtures.ProcCpuInfo)).MaxClockMhz);

    /// <summary>
    /// <c>cpu MHz</c> is the core's clock at the instant of the read, so under a scaling governor it is an
    /// idle 800 MHz. Reporting it as the maximum would put a number that means something else under the
    /// label — the near-miss this port refuses. The fixture's model name carries no "@ clock" suffix, so
    /// the only remaining candidate is the one that must not be used.
    /// </summary>
    [Fact]
    public void Read_NeverUsesTheInstantaneousCpuMhzAsTheMaximum() {
        var facts = CpuFacts.Read(WithCpuInfo(ProcFixtures.AmdCpuInfo));

        Assert.Equal(0, facts.MaxClockMhz);
    }

    [Fact]
    public void Read_ModelNameInMhz_IsNotScaled() {
        var facts = CpuFacts.Read(WithCpuInfo("processor\t: 0\nmodel name\t: Some CPU @ 800MHz\n"));

        Assert.Equal(800, facts.MaxClockMhz);
    }

    /// <summary>An unreadable <c>/proc</c> yields the "nothing known" record rather than a half-populated
    /// one, which is what lets both providers fall straight to their <c>.Unknown</c>.</summary>
    [Fact]
    public void Read_WithNoCpuinfo_IsNone() =>
        Assert.Equal(CpuFacts.None, CpuFacts.Read(new FakeProcFileSystem()));

    /// <summary>Sysfs writes the cache size with a unit suffix, never as a byte count — reading it bare
    /// would be off by three orders of magnitude.</summary>
    [Theory]
    [InlineData("8192K\n", 8192)]
    [InlineData("16M\n", 16384)]
    [InlineData("32768K", 32768)]
    public void L3CacheKilobytes_ConvertsTheSuffixedSize(string size, long expected) {
        var proc = new FakeProcFileSystem()
            .WithFile(CpuRoot + "/cpu0/cache/index3/level", "3\n")
            .WithFile(CpuRoot + "/cpu0/cache/index3/size", size);

        Assert.Equal(expected, CpuFacts.L3CacheKilobytes(proc));
    }

    /// <summary>Only the level-3 entry counts: the same directory holds L1d, L1i and L2, and taking the
    /// first would report a 32 KB L1 as the L3.</summary>
    [Fact]
    public void L3CacheKilobytes_IgnoresTheLowerLevels() {
        var proc = new FakeProcFileSystem()
            .WithFile(CpuRoot + "/cpu0/cache/index0/level", "1\n")
            .WithFile(CpuRoot + "/cpu0/cache/index0/size", "32K\n")
            .WithFile(CpuRoot + "/cpu0/cache/index2/level", "2\n")
            .WithFile(CpuRoot + "/cpu0/cache/index2/size", "512K\n")
            .WithFile(CpuRoot + "/cpu0/cache/index3/level", "3\n")
            .WithFile(CpuRoot + "/cpu0/cache/index3/size", "12288K\n");

        Assert.Equal(12288, CpuFacts.L3CacheKilobytes(proc));
    }

    /// <summary>A VM usually describes no caches at all; 0 is what renders "—".</summary>
    [Fact]
    public void L3CacheKilobytes_WithNoCacheTree_IsZero() =>
        Assert.Equal(0, CpuFacts.L3CacheKilobytes(new FakeProcFileSystem()));
}
