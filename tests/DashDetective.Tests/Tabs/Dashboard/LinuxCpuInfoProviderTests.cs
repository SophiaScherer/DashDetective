using DashDetective.Tabs.Dashboard;
using DashDetective.Tests.Fakes;
using System;
using System.Threading.Tasks;
using Xunit;

namespace DashDetective.Tests.Tabs.Dashboard;

/// <summary>Covers <see cref="LinuxCpuInfoProvider"/>'s mapping onto <see cref="CpuStaticInfo"/> — the
/// derivation itself belongs to <c>CpuFacts</c> and is covered there. What is pinned here is the two
/// substitutions it makes, and the one it does not.</summary>
public class LinuxCpuInfoProviderTests {
    private static Task<CpuStaticInfo> Read(FakeProcFileSystem proc) =>
        new LinuxCpuInfoProvider(proc).GetAsync();

    private static FakeProcFileSystem WithCpuInfo(string body) =>
        new FakeProcFileSystem().WithFile("/proc/cpuinfo", body);

    [Fact]
    public async Task GetAsync_ReportsTheModelNameAndCounts() {
        var info = await Read(WithCpuInfo(ProcFixtures.AmdCpuInfo));

        Assert.Equal("AMD Ryzen 5 7600X 4-Core Processor", info.Name);
        Assert.Equal(4, info.PhysicalCores);
        Assert.Equal(8, info.LogicalCores);
    }

    /// <summary>An ARM <c>cpuinfo</c> carries none of the x86 keys, but its blocks are still one per
    /// logical processor — the count survives even when the name and topology do not.</summary>
    [Fact]
    public async Task GetAsync_WithNoRecognisedKeys_StillCountsTheBlocks() {
        var info = await Read(WithCpuInfo(
            "processor\t: 0\nCPU implementer\t: 0x41\n\nprocessor\t: 1\nCPU implementer\t: 0x41\n"));

        Assert.Equal(2, info.LogicalCores);
        Assert.Equal("Unknown processor", info.Name);
    }

    /// <summary>With nothing readable the shared placeholder still reports the runtime's processor count,
    /// so the tile never claims a machine has zero cores.</summary>
    [Fact]
    public async Task GetAsync_WithNoCpuinfo_StillReportsTheRuntimeProcessorCount() =>
        Assert.Equal(Environment.ProcessorCount, (await Read(new FakeProcFileSystem())).LogicalCores);

    /// <summary>An architecture with no <c>model name</c> gets the placeholder, not a blank subtitle.</summary>
    [Fact]
    public async Task GetAsync_WithNoModelName_ReportsThePlaceholder() =>
        Assert.Equal("Unknown processor", (await Read(WithCpuInfo("processor\t: 0\n"))).Name);

    /// <summary>With no cpufreq and no clock in the model name — an AMD part in a VM — the clock stays 0
    /// rather than borrowing the block's instantaneous <c>cpu MHz</c>. 0 is what the tile renders as "—".</summary>
    [Fact]
    public async Task GetAsync_WithNoClockSource_LeavesTheClockAtZero() =>
        Assert.Equal(0, (await Read(WithCpuInfo(ProcFixtures.AmdCpuInfo))).MaxClockMhz);

    /// <summary>Physical cores stay 0 rather than borrowing the logical count when the topology keys are
    /// absent.</summary>
    [Fact]
    public async Task GetAsync_WithNoTopologyKeys_LeavesPhysicalCoresAtZero() =>
        Assert.Equal(0, (await Read(WithCpuInfo("processor\t: 0\nprocessor\t: 1\n"))).PhysicalCores);

    [Fact]
    public async Task GetAsync_ReportsTheClockFromTheModelName() =>
        Assert.Equal(3600, (await Read(WithCpuInfo(ProcFixtures.ProcCpuInfo))).MaxClockMhz);

    /// <summary>An unreadable <c>/proc</c> falls to the shared placeholder record rather than throwing.</summary>
    [Fact]
    public async Task GetAsync_WithNoCpuinfo_ReportsUnknown() =>
        Assert.Same(CpuStaticInfo.Unknown, await Read(new FakeProcFileSystem()));
}
