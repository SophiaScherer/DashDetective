using DashDetective.Services.SystemMetrics;
using DashDetective.Tests.Fakes;
using System;
using System.Linq;
using Xunit;

namespace DashDetective.Tests.Services.SystemMetrics;

/// <summary>Covers <see cref="LinuxProcessorFrequencySampler"/>: the <c>cpufreq</c> reading, the
/// <c>/proc/cpuinfo</c> fallback a VM actually exercises, and the both-absent case. Also pins
/// <see cref="IProcessorFrequencySampler.ForCurrentPlatform"/>'s dispatch.</summary>
public class LinuxProcessorFrequencySamplerTests {
    private const string CpuInfoPath = "/proc/cpuinfo";

    private static FakeProcFileSystem WithScalingFrequencies(params int[] kHzPerCore) {
        var proc = new FakeProcFileSystem();
        for (var core = 0; core < kHzPerCore.Length; core++) {
            proc.WithFile(
                "/sys/devices/system/cpu/cpu" + core + "/cpufreq/scaling_cur_freq",
                kHzPerCore[core].ToString());
        }

        return proc;
    }

    [Fact]
    public void Sample_CpuFreq_ReportsTheMeanInMhz() {
        // 3.6 GHz and 2.4 GHz in kHz → a 3.0 GHz mean.
        var sampler = new LinuxProcessorFrequencySampler(WithScalingFrequencies(3_600_000, 2_400_000));

        Assert.Equal(new ProcessorClockSample(PercentOfBase: 0, AbsoluteMhz: 3000), sampler.Sample());
    }

    /// <summary>Linux has no dependable base clock to divide by, so the ratio field stays empty and the
    /// formatter takes the absolute path.</summary>
    [Fact]
    public void Sample_NeverReportsARatio() {
        var sampler = new LinuxProcessorFrequencySampler(WithScalingFrequencies(3_600_000));

        Assert.Equal(0, sampler.Sample().PercentOfBase);
    }

    /// <summary>The usual VirtualBox shape: no <c>cpufreq</c> at all, because the guest does not control
    /// the clock. This is the path that actually renders on the VM.</summary>
    [Fact]
    public void Sample_NoCpuFreq_FallsBackToCpuInfo() {
        var proc = new FakeProcFileSystem().WithFile(CpuInfoPath, ProcFixtures.ProcCpuInfo);

        // 3600 and 2400 MHz across the fixture's two cores.
        Assert.Equal(3000, new LinuxProcessorFrequencySampler(proc).Sample().AbsoluteMhz);
    }

    /// <summary>The real <c>/proc/cpuinfo</c> separates key from value with tabs; the fixture keeps them,
    /// so a parser matching on a fixed layout fails here rather than on the VM.</summary>
    [Fact]
    public void Sample_CpuInfoIsTabSeparated() {
        Assert.Contains('\t', ProcFixtures.ProcCpuInfo);
    }

    /// <summary>A partial <c>cpufreq</c> tree (a core offline, or its governor file unreadable) still
    /// yields a reading from the cores that answered rather than falling all the way through.</summary>
    [Fact]
    public void Sample_PartialCpuFreq_AveragesTheCoresThatAnswered() {
        var proc = WithScalingFrequencies(3_000_000, 1_000_000)
            .WithFile("/sys/devices/system/cpu/cpu2/cpufreq/scaling_cur_freq", "")
            .WithFile(CpuInfoPath, ProcFixtures.ProcCpuInfo);

        Assert.Equal(2000, new LinuxProcessorFrequencySampler(proc).Sample().AbsoluteMhz);
    }

    /// <summary>Non-<c>cpuN</c> entries in the cpu directory (<c>cpufreq</c>, <c>cpuidle</c>, <c>online</c>)
    /// are not cores and must not be probed as such.</summary>
    [Fact]
    public void Sample_IgnoresNonCoreEntries() {
        var proc = WithScalingFrequencies(2_000_000)
            .WithFile("/sys/devices/system/cpu/cpufreq/boost", "1")
            .WithFile("/sys/devices/system/cpu/cpuidle/current_driver", "acpi_idle")
            .WithFile("/sys/devices/system/cpu/online", "0-3");

        Assert.Equal(2000, new LinuxProcessorFrequencySampler(proc).Sample().AbsoluteMhz);
        Assert.DoesNotContain(proc.Reads, path => path.Contains("cpuidle"));
    }

    [Fact]
    public void Sample_NeitherSource_ReturnsDefault() {
        Assert.Equal(default, new LinuxProcessorFrequencySampler(new FakeProcFileSystem()).Sample());
    }

    [Fact]
    public void Sample_CpuInfoWithoutAClockLine_ReturnsDefault() {
        var proc = new FakeProcFileSystem().WithFile(CpuInfoPath,
            string.Join('\n', ["processor\t: 0", "model name\t: Cortex-A72", "BogoMIPS\t: 108.00"]));

        Assert.Equal(default, new LinuxProcessorFrequencySampler(proc).Sample());
    }

    /// <summary>Stateless, unlike the two <c>/proc/stat</c> samplers — every call is a fresh read, so two
    /// calls with the same fixture agree.</summary>
    [Fact]
    public void Sample_IsStateless() {
        var sampler = new LinuxProcessorFrequencySampler(WithScalingFrequencies(3_600_000));

        Assert.Equal(sampler.Sample(), sampler.Sample());
    }

    [Fact]
    public void ForCurrentPlatform_PicksThisPlatformsReader() {
        using var sampler = IProcessorFrequencySampler.ForCurrentPlatform();

        if (OperatingSystem.IsWindows())
            Assert.IsType<WindowsProcessorFrequencySampler>(sampler);
        else if (OperatingSystem.IsLinux())
            Assert.IsType<LinuxProcessorFrequencySampler>(sampler);
        else
            Assert.IsType<UnsupportedProcessorFrequencySampler>(sampler);
    }
}
