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

    /// <summary>With no <c>pci.ids</c> the spec rows are "—", but the card still renders.</summary>
    [Fact]
    public async Task GetAsync_NoPciIds_LeavesTheCatalogueRowsBlank() {
        var adapter = Assert.Single((await Read(new FakeProcFileSystem().WithAmdgpuCard())).Adapters);

        Assert.Equal("—", adapter.CudaCores);
        Assert.Equal("—", adapter.BoostClock);
        Assert.Equal("—", adapter.Bus);
    }

    /// <summary>A <c>pci.ids</c> name carrying a model token fills the spec rows from the catalogue.</summary>
    [Fact]
    public async Task GetAsync_PciIdsNamesTheCard_FillsTheCatalogueRows() {
        var adapter = Assert.Single(
            (await Read(new FakeProcFileSystem().WithNvidiaCard().WithPciIds())).Adapters);

        Assert.Equal("NVIDIA GeForce RTX 3060 Lite Hash Rate", adapter.Name);
        // The blob publishes no VRAM, so this card's Memory row is the catalogue's too.
        Assert.Equal("12 GB GDDR6", adapter.Memory);
        Assert.Equal("3,584", adapter.CudaCores);
        Assert.Equal("1.78 GHz", adapter.BoostClock);
        Assert.Equal("PCIe 4.0 x16", adapter.Bus);
    }

    /// <summary>A named card the catalogue does not list keeps its blank spec rows.</summary>
    [Fact]
    public async Task GetAsync_NamedButUnlistedCard_KeepsTheCatalogueRowsBlank() {
        var adapter = Assert.Single(
            (await Read(new FakeProcFileSystem().WithAmdgpuCard().WithPciIds())).Adapters);

        Assert.Equal("AMD Radeon RX 6700/6700 XT/6750 XT / 6800M/6850M XT", adapter.Name);
        Assert.Equal("—", adapter.CudaCores);
        Assert.Equal("—", adapter.Bus);
    }

    /// <summary>A device id that ships as two models takes neither one's datasheet.</summary>
    [Fact]
    public async Task GetAsync_MultiModelName_SkipsTheCatalogue() {
        var proc = new FakeProcFileSystem().WithAmdgpuCard()
            .WithFile("/sys/class/drm/card0/device/device", "0x747e\n")
            .WithFile("/usr/share/hwdata/pci.ids",
                "1002  Advanced Micro Devices, Inc. [AMD/ATI]\n\t747e  Navi 32 [Radeon RX 7700 XT / 7800 XT]\n");

        var adapter = Assert.Single((await Read(proc)).Adapters);

        Assert.Equal("AMD Radeon RX 7700 XT / 7800 XT", adapter.Name);
        Assert.Equal("—", adapter.BoostClock);
        Assert.Equal("—", adapter.Bus);
        Assert.Equal("16 GB", adapter.Memory);
    }

    [Theory]
    [InlineData("NVIDIA GeForce RTX 3060 Lite Hash Rate", true)]
    [InlineData("", true)]
    [InlineData("AMD Radeon RX 7700 XT / 7800 XT", false)]
    [InlineData("AMD Radeon RX 6600/6600 XT/6600M", false)]
    public void NamesOneModel_FlagsATableNameListingSeveralModels(string name, bool expected) {
        Assert.Equal(expected, LinuxGraphicsInfoProvider.NamesOneModel(name));
    }

    /// <summary>VRAM comes from the driver, so a named card keeps the figure sysfs published.</summary>
    [Fact]
    public async Task GetAsync_PciIdsPresent_KeepsTheDriverReportedVram() {
        var adapter = Assert.Single(
            (await Read(new FakeProcFileSystem().WithAmdgpuCard().WithPciIds())).Adapters);

        Assert.Equal("16 GB", adapter.Memory);
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
