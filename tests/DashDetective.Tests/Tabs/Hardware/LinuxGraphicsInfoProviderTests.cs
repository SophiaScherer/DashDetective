using DashDetective.Tabs.Hardware;
using DashDetective.Tests.Fakes;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace DashDetective.Tests.Tabs.Hardware;

/// <summary>Covers <see cref="LinuxGraphicsInfoProvider"/>: the one row sysfs can genuinely fill (VRAM), the
/// rows it honestly cannot, and that a card still renders when every spec row is blank.</summary>
public class LinuxGraphicsInfoProviderTests {
    private static Task<GraphicsInfo> Read(FakeProcFileSystem proc) =>
        new LinuxGraphicsInfoProvider(proc).GetAsync();

    [Fact]
    public async Task GetAsync_ReportsTheCardWithItsVram() {
        var info = await Read(new FakeProcFileSystem().WithAmdgpuCard());

        var adapter = Assert.Single(info.Adapters);
        Assert.Equal("AMD amdgpu (1002:73df)", adapter.Name);
        Assert.Equal("16 GB", adapter.Memory);
    }

    /// <summary>The kernel names an adapter by its PCI ids, so the spec catalogue has nothing to match and
    /// these rows have no source. "—" is the honest answer, and the point of checking it is that the card
    /// still renders rather than being dropped for being mostly empty.</summary>
    [Fact]
    public async Task GetAsync_LeavesTheRowsSysfsCannotSupplyBlank() {
        var adapter = Assert.Single((await Read(new FakeProcFileSystem().WithAmdgpuCard())).Adapters);

        Assert.Equal("—", adapter.CudaCores);
        Assert.Equal("—", adapter.BoostClock);
        Assert.Equal("—", adapter.Bus);
    }

    /// <summary>An out-of-tree module publishes its own version; an in-tree one does not, and borrowing the
    /// kernel release would be a different fact under this label.</summary>
    [Fact]
    public async Task GetAsync_ReportsTheModuleVersionOnlyWhereThereIsOne() {
        var info = await Read(new FakeProcFileSystem().WithAmdgpuCard().WithNvidiaCard());

        var byName = info.Adapters.ToDictionary(a => a.Name, a => a.Driver);
        Assert.Equal("550.107.02", byName["NVIDIA nvidia (10de:2504)"]);
        Assert.Equal("—", byName["AMD amdgpu (1002:73df)"]);
    }

    /// <summary>A discrete card beside an integrated one gets a card each, as on Windows.</summary>
    [Fact]
    public async Task GetAsync_ReportsEveryAdapter() {
        var info = await Read(new FakeProcFileSystem().WithAmdgpuCard().WithNvidiaCard());

        Assert.Equal(
            ["AMD amdgpu (1002:73df)", "NVIDIA nvidia (10de:2504)"],
            info.Adapters.Select(a => a.Name));
    }

    /// <summary>The NVIDIA blob publishes no VRAM in sysfs, so that row blanks while the card still
    /// shows.</summary>
    [Fact]
    public async Task GetAsync_NoVramPublished_LeavesTheMemoryRowBlank() {
        var adapter = Assert.Single((await Read(new FakeProcFileSystem().WithNvidiaCard())).Adapters);

        Assert.Equal("—", adapter.Memory);
    }

    [Fact]
    public async Task GetAsync_NoDrmTree_IsUnknown() {
        Assert.Same(GraphicsInfo.Unknown, await Read(new FakeProcFileSystem()));
    }

    /// <summary>A placeholder DRM device is not an adapter and must not get a card of its own.</summary>
    [Fact]
    public async Task GetAsync_SkipsSoftwareDevices() {
        var proc = new FakeProcFileSystem()
            .WithFile("/sys/class/drm/card0/device/vendor", "0x1234\n")
            .WithLink("/sys/class/drm/card0/device/driver", "/sys/bus/pci/drivers/vkms");

        Assert.Same(GraphicsInfo.Unknown, await Read(proc));
    }

    /// <summary>The real constructor reads the live filesystem; on a box with no <c>/sys</c> that must be
    /// the Unknown record rather than a throw.</summary>
    [Fact]
    public async Task GetAsync_RealFileSystem_SoftFailsToUnknown() {
        if (System.OperatingSystem.IsLinux())
            return;

        Assert.Same(GraphicsInfo.Unknown, await new LinuxGraphicsInfoProvider().GetAsync());
    }
}
