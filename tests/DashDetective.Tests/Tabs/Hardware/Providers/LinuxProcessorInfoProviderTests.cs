using DashDetective.Tabs.Hardware;
using DashDetective.Tests.Fakes;
using System;
using System.Threading.Tasks;
using Xunit;

namespace DashDetective.Tests.Tabs.Hardware.Providers;

/// <summary>Covers <see cref="LinuxProcessorInfoProvider"/>'s mapping onto <see cref="ProcessorInfo"/> —
/// the counts and clock come from <c>CpuFacts</c> and are covered there. What is pinned here is the
/// formatting shared with the WMI arm, the cache read, and the row Linux permanently cannot fill.</summary>
public class LinuxProcessorInfoProviderTests {
    private const string CpuRoot = "/sys/devices/system/cpu";

    private static Task<ProcessorInfo> Read(FakeProcFileSystem proc) =>
        new LinuxProcessorInfoProvider(proc).GetAsync();

    private static FakeProcFileSystem WithCpuInfo(string body) =>
        new FakeProcFileSystem().WithFile("/proc/cpuinfo", body);

    [Fact]
    public async Task GetAsync_ReportsTheNameAndCounts() {
        var info = await Read(WithCpuInfo(ProcFixtures.AmdCpuInfo));

        Assert.Equal("AMD Ryzen 5 7600X 4-Core Processor", info.Name);
        Assert.Equal("4", info.Cores);
        Assert.Equal("8", info.LogicalProcessors);
    }

    /// <summary>Formatted through the same <c>ProcessorSpecFormatter</c> the WMI arm uses, so the row
    /// reads identically on both platforms: KB in, whole MB out.</summary>
    [Fact]
    public async Task GetAsync_FormatsTheL3CacheAsWholeMegabytes() {
        var proc = WithCpuInfo(ProcFixtures.ProcCpuInfo)
            .WithFile(CpuRoot + "/cpu0/cache/index3/level", "3\n")
            .WithFile(CpuRoot + "/cpu0/cache/index3/size", "12288K\n");

        Assert.Equal("12 MB", (await Read(proc)).CacheL3);
    }

    /// <summary>A VM usually describes no caches, which is a "—" rather than a "0 MB".</summary>
    [Fact]
    public async Task GetAsync_WithNoCacheTree_ReportsADashForL3() =>
        Assert.Equal("—", (await Read(WithCpuInfo(ProcFixtures.ProcCpuInfo))).CacheL3);

    /// <summary>The base half comes from the model name's rated clock; the boost half has no source on
    /// either platform and stays "—" unless the catalog knows this CPU.</summary>
    [Fact]
    public async Task GetAsync_ComposesBaseBoostFromTheRatedClock() =>
        Assert.StartsWith("3.6", (await Read(WithCpuInfo(ProcFixtures.ProcCpuInfo))).BaseBoost);

    /// <summary>
    /// Linux has no socket source at all: the designation lives in SMBIOS type 4, which the kernel does not
    /// surface under <c>/sys/class/dmi/id</c> — only <c>dmidecode</c> reading <c>/dev/mem</c> as root can
    /// see it. So the row is the rated socket of the part the machine named, and nothing here reads sysfs
    /// for it.
    /// </summary>
    [Fact]
    public async Task GetAsync_ReportsTheCatalogSocket_SinceSysfsHasNone() =>
        Assert.Equal("AM5", (await Read(WithCpuInfo(ProcFixtures.AmdCpuInfo))).Socket);

    /// <summary>A part the catalog does not list has no second source either, so the row stays blank rather
    /// than borrowing a similarly-named part's socket.</summary>
    [Fact]
    public async Task GetAsync_UnlistedPart_ReportsNoSocket() =>
        Assert.Equal("—", (await Read(WithCpuInfo(ProcFixtures.ProcCpuInfo))).Socket);

    /// <summary>The VM case the datasheet fallback exists for: a guest describes no cache topology and gets
    /// no <c>cpufreq</c> policy, but it does report the host chip's model name — so both rows fill from the
    /// part rather than sitting blank beside a named processor.</summary>
    [Fact]
    public async Task GetAsync_NoCacheTreeOrCpufreq_FillsFromTheCatalog() {
        var info = await Read(WithCpuInfo(ProcFixtures.AmdCpuInfo));

        Assert.Equal("32 MB", info.CacheL3);
        Assert.Equal("4.7 / 5.3 GHz", info.BaseBoost);
    }

    /// <summary>The machine's own cache size wins over the datasheet — a fallback, never an override.</summary>
    [Fact]
    public async Task GetAsync_WithACacheTree_PrefersTheMachinesOwnSize() {
        var proc = WithCpuInfo(ProcFixtures.AmdCpuInfo)
            .WithFile(CpuRoot + "/cpu0/cache/index3/level", "3\n")
            .WithFile(CpuRoot + "/cpu0/cache/index3/size", "16384K\n");

        Assert.Equal("16 MB", (await Read(proc)).CacheL3);
    }

    /// <summary>The card's own placeholder, not the Dashboard's — the two consumers of <c>CpuFacts</c>
    /// deliberately differ here.</summary>
    [Fact]
    public async Task GetAsync_WithNoModelName_ReportsADashRatherThanTheDashboardsPlaceholder() {
        var info = await Read(WithCpuInfo("processor\t: 0\n"));

        Assert.Equal("—", info.Name);
        Assert.Equal("—", info.Cores);
    }

    [Fact]
    public async Task GetAsync_WithNoCpuinfo_ReportsUnknown() =>
        Assert.Same(ProcessorInfo.Unknown, await Read(new FakeProcFileSystem()));

    /// <summary>Both cards read the same derivation, so a machine cannot show one core count on the
    /// Dashboard and another on the Hardware tab.</summary>
    [Fact]
    public async Task GetAsync_AgreesWithTheDashboardTileOnTheCounts() {
        var proc = WithCpuInfo(ProcFixtures.AmdCpuInfo);

        var card = await Read(proc);
        var tile = await new DashDetective.Tabs.Dashboard.LinuxCpuInfoProvider(proc).GetAsync();

        Assert.Equal(tile.PhysicalCores.ToString(), card.Cores);
        Assert.Equal(tile.LogicalCores.ToString(), card.LogicalProcessors);
        Assert.Equal(tile.Name, card.Name);
    }
}
