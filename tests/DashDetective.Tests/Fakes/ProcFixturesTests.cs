using Xunit;

namespace DashDetective.Tests.Fakes;

/// <summary>
/// Covers the GPU and hwmon fixtures' <i>shape</i> — that each stages a tree the later providers can
/// actually walk. The fixtures describe kernel layouts nobody here can run, so a staging slip (a wrong
/// symlink target, a file one level too deep) would otherwise surface as a provider bug in a later
/// milestone rather than as a fixture bug now.
/// </summary>
public class ProcFixturesTests {
    private const string AmdgpuPciPath = "/sys/devices/pci0000:00/0000:00:01.1/0000:03:00.0";
    private const string NvmeControllerPath = "/sys/devices/pci0000:00/0000:00:1d.0/0000:02:00.0/nvme/nvme0";
    private const string ScsiTargetPath = "/sys/devices/pci0000:00/0000:00:1f.2/ata1/host0/target0:0:0/0:0:0:0";

    [Fact]
    public void AmdgpuCard_IsListedUnderDrmWithItsRenderNode() {
        var fs = new FakeProcFileSystem().WithAmdgpuCard();

        // The render node is listed alongside the card: a walk that does not skip it counts the GPU twice.
        Assert.Equal(["card0", "renderD128"], fs.ListDirectory("/sys/class/drm"));
    }

    [Fact]
    public void AmdgpuCard_DeviceLinkResolvesToItsPciAddress() {
        var fs = new FakeProcFileSystem().WithAmdgpuCard();

        Assert.Equal(AmdgpuPciPath, fs.ResolveLink("/sys/class/drm/card0/device"));
        Assert.Equal("/sys/bus/pci/drivers/amdgpu", fs.ResolveLink("/sys/class/drm/card0/device/driver"));
    }

    [Fact]
    public void AmdgpuCard_ReportsUtilisationVramAndItsOwnHwmon() {
        var fs = new FakeProcFileSystem().WithAmdgpuCard();

        Assert.Equal("0x1002\n", fs.ReadAllText("/sys/class/drm/card0/device/vendor"));
        Assert.Equal("37\n", fs.ReadAllText("/sys/class/drm/card0/device/gpu_busy_percent"));
        Assert.Equal("17179869184\n", fs.ReadAllText("/sys/class/drm/card0/device/mem_info_vram_total"));
        Assert.Equal(["hwmon4"], fs.ListDirectory("/sys/class/drm/card0/device/hwmon"));
        Assert.Equal("amdgpu\n", fs.ReadAllText("/sys/class/drm/card0/device/hwmon/hwmon4/name"));
    }

    /// <summary>The degrade case the whole nvidia-smi decision rests on: the blob publishes the generic PCI
    /// ids and nothing else, so utilisation, VRAM and temperature all have no sysfs source.</summary>
    [Fact]
    public void NvidiaCard_HasPciIdsButNoUtilisationVramOrHwmon() {
        var fs = new FakeProcFileSystem().WithNvidiaCard();

        Assert.Equal("0x10de\n", fs.ReadAllText("/sys/class/drm/card1/device/vendor"));
        Assert.Null(fs.ReadAllText("/sys/class/drm/card1/device/gpu_busy_percent"));
        Assert.Null(fs.ReadAllText("/sys/class/drm/card1/device/mem_info_vram_total"));
        Assert.False(fs.Exists("/sys/class/drm/card1/device/hwmon"));
        Assert.Equal("550.107.02\n", fs.ReadAllText("/sys/class/drm/card1/device/driver/module/version"));
    }

    [Fact]
    public void BothCards_ComposeIntoOneTwoAdapterMachine() {
        var fs = new FakeProcFileSystem().WithAmdgpuCard().WithNvidiaCard();

        Assert.Equal(["card0", "card1", "renderD128"], fs.ListDirectory("/sys/class/drm"));
    }

    /// <summary>NVMe is the two-hop mapping: the hwmon's device is the controller, and the block device is
    /// a namespace child of it.</summary>
    [Fact]
    public void NvmeHwmon_ResolvesToAControllerWhoseChildIsTheNamespace() {
        var fs = new FakeProcFileSystem().WithNvmeHwmon();

        Assert.Equal("nvme\n", fs.ReadAllText("/sys/class/hwmon/hwmon2/name"));
        Assert.Equal(NvmeControllerPath, fs.ResolveLink("/sys/class/hwmon/hwmon2/device"));
        Assert.Equal(["model", "nvme0n1"], fs.ListDirectory(NvmeControllerPath));
        Assert.Equal("259:0\n", fs.ReadAllText(NvmeControllerPath + "/nvme0n1/dev"));
    }

    /// <summary>drivetemp is the other shape: a SCSI target with the block device under <c>block/</c>.</summary>
    [Fact]
    public void DrivetempHwmon_ResolvesToAScsiTargetWithABlockChild() {
        var fs = new FakeProcFileSystem().WithDrivetempHwmon();

        Assert.Equal("drivetemp\n", fs.ReadAllText("/sys/class/hwmon/hwmon3/name"));
        Assert.Equal(ScsiTargetPath, fs.ResolveLink("/sys/class/hwmon/hwmon3/device"));
        Assert.Equal(["sda"], fs.ListDirectory(ScsiTargetPath + "/block"));
        Assert.Equal("8:0\n", fs.ReadAllText(ScsiTargetPath + "/block/sda/dev"));
    }

    /// <summary>The drive sensors are not the low-numbered ones, which is what makes matching on
    /// <c>name</c> rather than index load-bearing.</summary>
    [Fact]
    public void HwmonFixtures_PutTheNonDriveSensorsFirst() {
        var fs = new FakeProcFileSystem().WithNonDriveHwmon().WithNvmeHwmon().WithDrivetempHwmon();

        Assert.Equal(["hwmon0", "hwmon1", "hwmon2", "hwmon3"], fs.ListDirectory("/sys/class/hwmon"));
        Assert.Equal("coretemp\n", fs.ReadAllText("/sys/class/hwmon/hwmon0/name"));
        Assert.Equal("acpitz\n", fs.ReadAllText("/sys/class/hwmon/hwmon1/name"));
    }
}
