using DashDetective.Services.SystemMetrics;
using DashDetective.Tabs.Dashboard;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace DashDetective.Tests.Services.SystemMetrics;

/// <summary>Covers <see cref="DeviceInventory.Compose"/>: the grouped instance list (CPU → Memory → GPU →
/// Disks → Network), the per-category <see cref="DeviceInventory.All"/> / <see cref="DeviceInventory.Primary"/>
/// accessors, multi-disk enumeration + ordering, and the static display identity (captions/specs).</summary>
public class DeviceInventoryTests {
    private static readonly CpuStaticInfo Cpu = new("Intel Core i9-14900K", 24, 32, 3200);
    private static readonly MemoryStaticInfo Memory = new(32, "DDR5", 6000, 2);

    private const string GpuLuid = "luid_0x00000000_0x0000e54b";
    private static readonly GpuAdapter[] Gpus = { new(GpuLuid, "NVIDIA GeForce RTX 4080", false, 0) };
    private static readonly IReadOnlySet<string> ActiveGpus = new HashSet<string> { GpuLuid };

    private static PhysicalDiskInfo Disk(int id, string model = "Test Disk", ulong sizeBytes = 0) =>
        new(id, model, "NVMe SSD", sizeBytes, true);

    private static VolumeInfo Vol(int disk, char letter, string label = "") =>
        new(disk, letter, label, "NTFS", 1000, 500);

    private static DeviceInventory Compose(
        PhysicalDiskInfo[] disks, VolumeInfo[] volumes, string netName = "Ethernet", string netSpec = "Intel I225-V") =>
        DeviceInventory.Compose(Cpu, Memory, Gpus, ActiveGpus, disks, volumes, netName, netSpec);

    [Fact]
    public void Compose_SingleOfEach_ProducesGroupedOrder() {
        var inv = Compose(new[] { Disk(0) }, new[] { Vol(0, 'C') });

        Assert.Equal(
            new[] { DeviceCategory.Cpu, DeviceCategory.Memory, DeviceCategory.Gpu, DeviceCategory.Disk, DeviceCategory.Network },
            inv.Instances.Select(d => d.Category));
    }

    [Fact]
    public void Compose_MultipleDisks_EnumeratesAllInDiskNumberOrder() {
        var inv = Compose(
            new[] { Disk(2), Disk(0), Disk(1) },
            new[] { Vol(0, 'C'), Vol(1, 'D'), Vol(2, 'E') });

        var disks = inv.All(DeviceCategory.Disk);
        Assert.Equal(new int?[] { 0, 1, 2 }, disks.Select(d => d.DiskNumber));
        // Disk instances sit contiguously between GPU and Network.
        Assert.Equal(3, inv.Instances.Count(d => d.Category == DeviceCategory.Disk));
    }

    [Fact]
    public void Primary_ReturnsFirstOfCategory_OrNullWhenAbsent() {
        var inv = Compose(new[] { Disk(0), Disk(1) }, new[] { Vol(0, 'C'), Vol(1, 'D') });

        Assert.Equal(0, inv.Primary(DeviceCategory.Disk)!.DiskNumber);
        Assert.Equal("CPU", inv.Primary(DeviceCategory.Cpu)!.Name);
    }

    [Fact]
    public void Primary_NoDisks_IsNull() {
        var inv = Compose(new PhysicalDiskInfo[0], new VolumeInfo[0]);

        Assert.Null(inv.Primary(DeviceCategory.Disk));
        Assert.Empty(inv.All(DeviceCategory.Disk));
        // The four singleton categories are still present.
        Assert.Equal(4, inv.Instances.Count);
    }

    [Fact]
    public void Compose_CpuIdentity_MatchesRailCaptions() {
        var cpu = Compose(new PhysicalDiskInfo[0], new VolumeInfo[0]).Primary(DeviceCategory.Cpu)!;

        Assert.Equal("cpu", cpu.Id);
        Assert.Equal("24 cores · 3.2 GHz", cpu.Sub);
        Assert.Equal("Intel Core i9-14900K", cpu.Spec);
    }

    [Fact]
    public void Compose_DiskIdentity_UsesVolumeNameTypeLabelAndStableId() {
        var disk = Compose(new[] { Disk(0, "Samsung SSD 990 Pro", 2_000_000_000_000) },
                           new[] { Vol(0, 'C', "Windows") }).Primary(DeviceCategory.Disk)!;

        Assert.Equal("disk:0", disk.Id);
        Assert.Equal("Windows (C:)", disk.Name);       // from StorageComposer's name logic
        Assert.Equal("NVMe SSD", disk.Sub);            // media/bus type label
        Assert.StartsWith("Samsung SSD 990 Pro", disk.Spec);
        Assert.Contains("TB", disk.Spec);
    }

    [Fact]
    public void Compose_GpuAndNetworkIdentity() {
        var inv = Compose(new PhysicalDiskInfo[0], new VolumeInfo[0], netName: "Wi-Fi", netSpec: "Intel AX211");

        var gpu = inv.Primary(DeviceCategory.Gpu)!;
        Assert.Equal("GPU", gpu.Name);                 // a single GPU keeps the plain label
        Assert.Equal("GeForce RTX 4080", gpu.Sub);     // vendor prefix trimmed
        Assert.Equal("NVIDIA GeForce RTX 4080", gpu.Spec);
        Assert.Equal(GpuLuid, gpu.GpuLuid);
        Assert.Equal($"gpu:{GpuLuid}", gpu.Id);

        var net = inv.Primary(DeviceCategory.Network)!;
        Assert.Equal("Wi-Fi", net.Name);
        Assert.Equal("Intel AX211", net.Spec);
    }

    [Fact]
    public void Compose_MultipleGpus_EnumeratesRealAdaptersIndexedByLuid() {
        var gpus = new[] {
            new GpuAdapter("luid_0x00000000_0x0000e54b", "NVIDIA GeForce RTX 3060", false, 0),
            new GpuAdapter("luid_0x00000000_0x0000f83d", "AMD Radeon(TM) Graphics", false, 0),
        };
        var active = new HashSet<string> { "luid_0x00000000_0x0000e54b", "luid_0x00000000_0x0000f83d" };
        var inv = DeviceInventory.Compose(
            Cpu, Memory, gpus, active, new[] { Disk(0) }, new[] { Vol(0, 'C') }, "Ethernet", "");

        var g = inv.All(DeviceCategory.Gpu);
        // Several GPUs are indexed "GPU 0"/"GPU 1" with the model in the sub and a stable LUID-keyed id.
        Assert.Equal(new[] { "GPU 0", "GPU 1" }, g.Select(x => x.Name));
        Assert.Equal(
            new[] { "gpu:luid_0x00000000_0x0000e54b", "gpu:luid_0x00000000_0x0000f83d" },
            g.Select(x => x.Id));
        Assert.Equal("luid_0x00000000_0x0000e54b", g[0].GpuLuid);
        Assert.Equal("GeForce RTX 3060", g[0].Sub);
        // GPU instances sit contiguously between Memory and Disk.
        Assert.Equal(
            new[] { DeviceCategory.Cpu, DeviceCategory.Memory, DeviceCategory.Gpu, DeviceCategory.Gpu,
                    DeviceCategory.Disk, DeviceCategory.Network },
            inv.Instances.Select(d => d.Category));
    }

    [Fact]
    public void Compose_ExcludesSoftwareAndPhantomLuidGpus() {
        // Mirrors the dev box: DXGI lists the RTX 3060 under two LUIDs (only one is engine-active) plus a
        // software Basic Render Driver. Only the active, non-software adapter should survive.
        var gpus = new[] {
            new GpuAdapter("luid_0x00000000_0x0000e54b", "NVIDIA GeForce RTX 3060", false, 0),  // real + active
            new GpuAdapter("luid_0x00000000_0x0001ac07", "NVIDIA GeForce RTX 3060", false, 0),  // phantom: not active
            new GpuAdapter("luid_0x00000000_0x0000f7e0", "Microsoft Basic Render Driver", true, 0), // software
        };
        var active = new HashSet<string> { "luid_0x00000000_0x0000e54b", "luid_0x00000000_0x0000f7e0" };
        var inv = DeviceInventory.Compose(
            Cpu, Memory, gpus, active, new PhysicalDiskInfo[0], new VolumeInfo[0], "Ethernet", "");

        var g = inv.All(DeviceCategory.Gpu);
        Assert.Single(g);
        Assert.Equal("GPU", g[0].Name);   // one survivor → plain label
        Assert.Equal("luid_0x00000000_0x0000e54b", g[0].GpuLuid);
    }

    [Fact]
    public void Compose_NoActiveGpus_YieldsNoGpuInstances() {
        var inv = DeviceInventory.Compose(
            Cpu, Memory, Gpus, new HashSet<string>(), new PhysicalDiskInfo[0], new VolumeInfo[0], "Ethernet", "");

        Assert.Empty(inv.All(DeviceCategory.Gpu));
        Assert.Null(inv.Primary(DeviceCategory.Gpu));
    }
}
