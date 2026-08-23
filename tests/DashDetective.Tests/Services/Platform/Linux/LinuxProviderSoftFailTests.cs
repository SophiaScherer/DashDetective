using DashDetective.Services.SystemMetrics;
using DashDetective.Tabs.Dashboard;
using DashDetective.Tabs.Hardware;
using DashDetective.Tests.Fakes;
using System.Threading.Tasks;
using Xunit;

namespace DashDetective.Tests.Services.Platform.Linux;

/// <summary>
/// Pins the Linux providers' soft-fail contract: a pseudo-file that <b>denies</b> the read degrades to
/// the neutral value rather than throwing out of an async load.
///
/// These paths were unreachable from the suite until <see cref="FakeProcFileSystem.ThrowOn"/> existed:
/// the real <c>ProcFileSystem</c> swallows I/O failures itself and hands back <c>null</c>/empty, so every
/// provider's own <c>catch</c> sat behind a layer that could never let anything through.
///
/// <para><b>Every test here stages a fixture the provider would otherwise READ SUCCESSFULLY, and only
/// then denies it.</b> That is the whole design of this file. Denying an empty filesystem proves nothing
/// — an unstaged fake already yields the same <c>Unknown</c>/empty result through the ordinary
/// "nothing here" path, so such a test passes whether or not the catch exists. Each case below asserts
/// the neutral value against a fixture that demonstrably produces a real one when readable, which is
/// what makes the catch the only thing standing between them.</para>
///
/// One file rather than a case in each provider's own tests, for the same reason
/// <c>SamplerSoftFailTests</c> is one file: the contract is identical across all of them, and stating it
/// once is what makes a new provider that forgets it obvious.
/// </summary>
public class LinuxProviderSoftFailTests {
    private static readonly int Sda = DashDetective.Services.Platform.Linux.SysBlockFacts.Pack(8, 0);

    // ---- CPU identity: /proc/cpuinfo ----

    [Fact]
    public async Task LinuxCpuInfoProvider_ReadableCpuInfo_ReportsTheRealName() {
        var proc = new FakeProcFileSystem().WithFile("/proc/cpuinfo", ProcFixtures.AmdCpuInfo);

        var info = await new LinuxCpuInfoProvider(proc).GetAsync();

        Assert.Contains("AMD", info.Name);
    }

    [Fact]
    public async Task LinuxCpuInfoProvider_DeniedCpuInfo_ReportsTheUnknownSnapshot() {
        var proc = new FakeProcFileSystem()
            .WithFile("/proc/cpuinfo", ProcFixtures.AmdCpuInfo)
            .ThrowOn("/proc/cpuinfo");

        var info = await new LinuxCpuInfoProvider(proc).GetAsync();

        Assert.Equal(CpuStaticInfo.Unknown.Name, info.Name);
    }

    [Fact]
    public async Task LinuxProcessorInfoProvider_DeniedCpuInfo_ReportsTheUnknownCard() {
        var proc = new FakeProcFileSystem()
            .WithFile("/proc/cpuinfo", ProcFixtures.AmdCpuInfo)
            .ThrowOn("/proc/cpuinfo");

        Assert.Same(ProcessorInfo.Unknown, await new LinuxProcessorInfoProvider(proc).GetAsync());
    }

    // ---- Machine identity: /sys/class/dmi/id ----

    [Fact]
    public async Task LinuxMotherboardInfoProvider_ReadableDmi_ReportsTheRealBoard() {
        var info = await new LinuxMotherboardInfoProvider(new FakeProcFileSystem().WithVirtualBoxDmi())
            .GetAsync();

        Assert.NotSame(MotherboardInfo.Unknown, info);
    }

    [Fact]
    public async Task LinuxMotherboardInfoProvider_DeniedDmi_ReportsTheUnknownCard() {
        var proc = new FakeProcFileSystem().WithVirtualBoxDmi().ThrowOn("/sys/class/dmi");

        Assert.Same(MotherboardInfo.Unknown, await new LinuxMotherboardInfoProvider(proc).GetAsync());
    }

    [Fact]
    public async Task LinuxSystemInfoProvider_DeniedDmi_StillReportsAWholeSnapshot() {
        var proc = new FakeProcFileSystem().WithVirtualBoxDmi().ThrowOn("/sys/class/dmi");

        var info = await new LinuxSystemInfoProvider(proc).GetAsync();

        // Every field falls back independently, so a dead DMI tree cannot blank the whole panel.
        Assert.NotNull(info.Os);
        Assert.NotNull(info.Bios);
        Assert.NotNull(info.Motherboard);
    }

    // ---- GPUs: /sys/class/drm ----

    [Fact]
    public async Task LinuxGpuAdapterProvider_ReadableDrmTree_ReportsTheCard() {
        var adapters = await new LinuxGpuAdapterProvider(new FakeProcFileSystem().WithAmdgpuCard())
            .GetAsync();

        Assert.NotEmpty(adapters);
    }

    [Fact]
    public async Task LinuxGpuAdapterProvider_DeniedDrmTree_ReportsNoAdapters() {
        var proc = new FakeProcFileSystem().WithAmdgpuCard().ThrowOn("/sys/class/drm");

        Assert.Empty(await new LinuxGpuAdapterProvider(proc).GetAsync());
    }

    [Fact]
    public async Task LinuxGraphicsInfoProvider_DeniedDrmTree_ReportsTheUnknownCard() {
        var proc = new FakeProcFileSystem().WithAmdgpuCard().ThrowOn("/sys/class/drm");

        Assert.Same(GraphicsInfo.Unknown, await new LinuxGraphicsInfoProvider(proc).GetAsync());
    }

    // ---- Disks: /sys/block ----

    [Fact]
    public async Task LinuxPhysicalDiskProvider_ReadableBlockTree_ReportsTheDisk() {
        var proc = new FakeProcFileSystem().WithVirtualBoxBlockTree();

        var disks = await new LinuxPhysicalDiskProvider(new NoTemperature(), proc).GetAsync();

        Assert.NotEmpty(disks);
    }

    [Fact]
    public async Task LinuxPhysicalDiskProvider_DeniedBlockTree_ReportsNoDisks() {
        var proc = new FakeProcFileSystem().WithVirtualBoxBlockTree().ThrowOn("/sys/block");

        Assert.Empty(await new LinuxPhysicalDiskProvider(new NoTemperature(), proc).GetAsync());
    }

    [Fact]
    public async Task LinuxStorageInfoProvider_DeniedBlockTree_ReportsTheUnknownCard() {
        var proc = new FakeProcFileSystem().WithVirtualBoxBlockTree().ThrowOn("/sys/block");

        Assert.Same(StorageInfo.Unknown, await new LinuxStorageInfoProvider(proc).GetAsync());
    }

    // ---- Drive temperature: /sys/class/hwmon ----

    /// <summary>The positive control for the case below: this exact fixture reports 38 °C when readable
    /// (see <c>LinuxDiskTemperatureProviderTests</c>), so a null from it means the denial was handled.</summary>
    [Fact]
    public void LinuxDiskTemperatureProvider_ReadableHwmon_ReportsATemperature() {
        var proc = new FakeProcFileSystem().WithDrivetempHwmon().WithVirtualBoxBlockTree();

        Assert.Equal(38, new LinuxDiskTemperatureProvider(proc).ReadCelsius(Sda));
    }

    [Fact]
    public void LinuxDiskTemperatureProvider_DeniedHwmon_ReportsNoTemperature() {
        var proc = new FakeProcFileSystem()
            .WithDrivetempHwmon().WithVirtualBoxBlockTree().ThrowOn("/sys/class/hwmon");

        Assert.Null(new LinuxDiskTemperatureProvider(proc).ReadCelsius(Sda));
    }

    // ---- Volumes: /proc/mounts ----

    [Fact]
    public async Task LinuxVolumeProvider_DeniedMounts_ReportsNoVolumes() {
        var proc = new FakeProcFileSystem().WithVirtualBoxBlockTree().WithLvmRoot().ThrowOn("/proc/mounts");

        Assert.Empty(await new LinuxVolumeProvider(proc, new StubCapacity()).GetAsync());
    }

    private sealed class StubCapacity : DashDetective.Services.Platform.Linux.IVolumeCapacityReader {
        public DashDetective.Services.Platform.Linux.VolumeCapacity Read(string mountPoint) => new(1024, 512);
    }

    private sealed class NoTemperature : IDiskTemperatureProvider {
        public double? ReadCelsius(int deviceId) => null;
    }
}
