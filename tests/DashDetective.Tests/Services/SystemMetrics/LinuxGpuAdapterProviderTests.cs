using DashDetective.Services.SystemMetrics;
using DashDetective.Tests.Fakes;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace DashDetective.Tests.Services.SystemMetrics;

/// <summary>Covers <see cref="LinuxGpuAdapterProvider"/>: that a DRM card maps onto the same
/// <see cref="GpuAdapter"/> shape DXGI fills on Windows, and above all that the adapter's token is the
/// card's PCI address — the key the utilisation sampler has to arrive at independently.</summary>
public class LinuxGpuAdapterProviderTests {
    private static Task<System.Collections.Generic.IReadOnlyList<GpuAdapter>> Read(FakeProcFileSystem proc) =>
        new LinuxGpuAdapterProvider(proc).GetAsync();

    [Fact]
    public async Task GetAsync_MapsADrmCardOntoTheAdapterRecord() {
        var adapter = Assert.Single(await Read(new FakeProcFileSystem().WithAmdgpuCard()));

        Assert.Equal("0000:03:00.0", adapter.LuidToken);
        Assert.Equal("AMD amdgpu (1002:73df)", adapter.Name);
        Assert.False(adapter.IsSoftware);
        Assert.Equal(17179869184u, adapter.DedicatedVideoMemory);
    }

    /// <summary>PCI config space carries the two subsystem ids as one 32-bit field, device in the high
    /// half — the form <c>DXGI_ADAPTER_DESC1.SubSysId</c> reports. sysfs splits them, so the same card
    /// would otherwise read differently on the two platforms.</summary>
    [Fact]
    public async Task GetAsync_PacksTheSubsystemIdsTheWayDxgiReportsThem() {
        var adapter = Assert.Single(await Read(new FakeProcFileSystem().WithAmdgpuCard()));

        var pci = Assert.IsType<GpuPciId>(adapter.Pci);
        Assert.Equal(0x1002u, pci.VendorId);
        Assert.Equal(0x73dfu, pci.DeviceId);
        Assert.Equal(0x0e3b1002u, pci.SubSysId);
        Assert.Equal(0xc7u, pci.Revision);
    }

    [Fact]
    public async Task GetAsync_ReportsEveryCardWithADistinctToken() {
        var adapters = await Read(new FakeProcFileSystem().WithAmdgpuCard().WithNvidiaCard());

        Assert.Equal(["0000:03:00.0", "0000:01:00.0"], adapters.Select(a => a.LuidToken));
    }

    /// <summary>The NVIDIA blob publishes no VRAM in sysfs. 0 is what blanks the Performance tab's VRAM
    /// tile; a substituted figure would be a wrong number rather than an absent one.</summary>
    [Fact]
    public async Task GetAsync_NvidiaCard_ReportsNoVram() {
        var adapter = Assert.Single(await Read(new FakeProcFileSystem().WithNvidiaCard()));

        Assert.Equal("NVIDIA nvidia (10de:2504)", adapter.Name);
        Assert.Equal(0u, adapter.DedicatedVideoMemory);
    }

    [Fact]
    public async Task GetAsync_NoDrmTree_ReturnsEmpty() {
        Assert.Empty(await Read(new FakeProcFileSystem()));
    }

    /// <summary>The real constructor reads the live filesystem. On a Windows dev box <c>/sys</c> does not
    /// exist, which must be an empty list rather than a throw — the never-throw contract every provider
    /// in this codebase holds to.</summary>
    [Fact]
    public async Task GetAsync_RealFileSystem_SoftFailsToEmpty() {
        if (System.OperatingSystem.IsLinux())
            return;

        Assert.Empty(await new LinuxGpuAdapterProvider().GetAsync());
    }
}
