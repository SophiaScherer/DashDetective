using DashDetective.Services.Platform.Linux;
using DashDetective.Tests.Fakes;
using System.Linq;
using Xunit;

namespace DashDetective.Tests.Services.Platform.Linux;

/// <summary>Covers <see cref="DrmCardFacts"/>: the node filter that keeps one GPU from becoming several,
/// the PCI-address identity every GPU reader has to agree on, the hex id parse, and the name formatting
/// that has to stay informative for a card the bundled vendor table has never seen.</summary>
public class DrmCardFactsTests {
    private static FakeProcFileSystem Amd() => new FakeProcFileSystem().WithAmdgpuCard();

    /// <summary>The milestone's acceptance criterion in miniature: <c>/sys/class/drm</c> mixes cards with
    /// render nodes and one entry per connector, and counting any of those as an adapter turns a single
    /// GPU into three or four cards on the Dashboard.</summary>
    [Fact]
    public void Read_CountsCardsOnly_NotRenderNodesOrConnectors() {
        var proc = Amd()
            .WithFile("/sys/class/drm/card0-DP-1/status", "connected\n")
            .WithFile("/sys/class/drm/card0-HDMI-A-1/status", "disconnected\n")
            .WithFile("/sys/class/drm/version", "drm 1.1.0 20060810\n");

        Assert.Equal(["card0"], DrmCardFacts.Read(proc).Select(c => c.Name));
    }

    /// <summary>The join key. Every GPU reader derives it from this one place, because the inventory only
    /// builds a card for an adapter the enumeration and the sampler both report — two readers disagreeing
    /// yields no GPU at all, silently.</summary>
    [Fact]
    public void Read_KeysTheCardOnItsPciAddress() {
        var card = Assert.Single(DrmCardFacts.Read(Amd()));

        Assert.Equal("0000:03:00.0", card.PciAddress);
        Assert.Equal("0000:03:00.0", card.Key);
    }

    /// <summary>A DRM node with no PCI parent still needs a non-empty key, or it collides with every other
    /// keyless card in the inventory's dictionary.</summary>
    [Fact]
    public void Key_FallsBackToTheCardName_WhenThereIsNoPciAddress() {
        var proc = new FakeProcFileSystem()
            .WithFile("/sys/class/drm/card0/device/vendor", "0x1002\n")
            .WithFile("/sys/class/drm/card0/device/device", "0x73df\n");

        var card = Assert.Single(DrmCardFacts.Read(proc));

        Assert.Equal("", card.PciAddress);
        Assert.Equal("card0", card.Key);
    }

    [Fact]
    public void Read_ResolvesTheDriverAndPciIdsAndVram() {
        var card = Assert.Single(DrmCardFacts.Read(Amd()));

        Assert.Equal("amdgpu", card.Driver);
        Assert.Equal(0x1002u, card.VendorId);
        Assert.Equal(0x73dfu, card.DeviceId);
        Assert.Equal(0x0e3bu, card.SubsystemDeviceId);
        Assert.Equal(0xc7u, card.Revision);
        Assert.Equal(17179869184u, card.VramBytes);
        Assert.Equal("/sys/class/drm/card0/device/hwmon/hwmon4", card.HwmonPath);
    }

    /// <summary>The NVIDIA blob publishes the PCI ids and nothing else. Every absent source has to read as
    /// 0/"" so each consumer can render its own "—", rather than as a plausible zero.</summary>
    [Fact]
    public void Read_NvidiaCard_ReportsNoVramAndNoHwmon() {
        var card = Assert.Single(DrmCardFacts.Read(new FakeProcFileSystem().WithNvidiaCard()));

        Assert.Equal(0x10deu, card.VendorId);
        Assert.Equal("nvidia", card.Driver);
        Assert.Equal(0u, card.VramBytes);
        Assert.Equal("", card.HwmonPath);
    }

    /// <summary>A DRM node with no PCI identity has no name, no ids and no sensors — a card built from it
    /// renders blank, so it is not a card.</summary>
    [Fact]
    public void Read_SkipsNodesWithNoPciVendor() {
        var proc = new FakeProcFileSystem()
            .WithFile("/sys/class/drm/card0/device/uevent", "DRIVER=simple-framebuffer\n");

        Assert.Empty(DrmCardFacts.Read(proc));
    }

    [Fact]
    public void Read_OrdersCardsByKernelIndex_NotByName() {
        var proc = new FakeProcFileSystem();
        foreach (var index in new[] { 10, 2, 1 })
            proc.WithFile($"/sys/class/drm/card{index}/device/vendor", "0x1002\n");

        Assert.Equal(["card1", "card2", "card10"], DrmCardFacts.Read(proc).Select(c => c.Name));
    }

    [Fact]
    public void Read_BothCards_AreReportedWithDistinctKeys() {
        var cards = DrmCardFacts.Read(new FakeProcFileSystem().WithAmdgpuCard().WithNvidiaCard());

        Assert.Equal(["0000:03:00.0", "0000:01:00.0"], cards.Select(c => c.Key));
    }

    /// <summary>A paravirtualised GPU is the VM's real display adapter, so it must not be filtered the way
    /// DXGI's Basic Render Driver is — doing so leaves a VM with no GPU card at all.</summary>
    [Theory]
    [InlineData("vboxvideo", false)]
    [InlineData("virtio-gpu", false)]
    [InlineData("vmwgfx", false)]
    [InlineData("amdgpu", false)]
    [InlineData("simpledrm", true)]
    [InlineData("vkms", true)]
    public void IsSoftware_FlagsPlaceholderDevicesOnly(string driver, bool expected) {
        var proc = new FakeProcFileSystem()
            .WithFile("/sys/class/drm/card0/device/vendor", "0x80ee\n")
            .WithLink("/sys/class/drm/card0/device/driver", "/sys/bus/pci/drivers/" + driver);

        Assert.Equal(expected, Assert.Single(DrmCardFacts.Read(proc)).IsSoftware);
    }

    /// <summary>The kernel writes these with an <c>0x</c> prefix that <c>HexNumber</c> rejects, so a parser
    /// that omits the strip reads 0 for every id on every real machine.</summary>
    [Theory]
    [InlineData("0x1002\n", 0x1002u)]
    [InlineData("0X10DE\n", 0x10deu)]
    [InlineData("1002", 0x1002u)]
    [InlineData("", 0u)]
    [InlineData("not-a-number\n", 0u)]
    public void ParseHexId_ReadsThePrefixedForm(string text, uint expected) {
        Assert.Equal(expected, DrmCardFacts.ParseHexId(text));
    }

    [Fact]
    public void ParseHexId_MissingFile_IsZero() {
        Assert.Equal(0u, DrmCardFacts.ParseHexId(null));
    }

    /// <summary>The acceptance criterion: an id the bundled table has never seen degrades to raw hex, not
    /// to an empty card.</summary>
    [Theory]
    [InlineData(0x1002u, 0x73dfu, "amdgpu", "AMD amdgpu (1002:73df)")]
    [InlineData(0x10deu, 0x2504u, "nvidia", "NVIDIA nvidia (10de:2504)")]
    [InlineData(0x1002u, 0x73dfu, "", "AMD (1002:73df)")]
    [InlineData(0x1a2bu, 0x0001u, "mygpu", "mygpu (1a2b:0001)")]
    [InlineData(0x1a2bu, 0x0001u, "", "(1a2b:0001)")]
    public void FormatAdapterName_KeepsTheIdsWhateverIsKnown(
        uint vendorId, uint deviceId, string driver, string expected) {
        Assert.Equal(expected, DrmCardFacts.FormatAdapterName(vendorId, deviceId, driver));
    }

    [Fact]
    public void AdapterName_ComposesTheCardsOwnIdentity() {
        Assert.Equal("AMD amdgpu (1002:73df)", Assert.Single(DrmCardFacts.Read(Amd())).AdapterName);
    }

    [Fact]
    public void Read_EmptySysfs_YieldsNoCards() {
        Assert.Empty(DrmCardFacts.Read(new FakeProcFileSystem()));
    }
}
