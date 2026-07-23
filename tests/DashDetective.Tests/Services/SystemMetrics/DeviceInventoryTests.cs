using DashDetective.Services.SystemMetrics;
using DashDetective.Tabs.Dashboard;
using System.Linq;
using Xunit;

namespace DashDetective.Tests.Services.SystemMetrics;

/// <summary>Covers <see cref="DeviceInventory.Compose"/>: the grouped instance list (CPU → Memory → GPU →
/// Disks → Network), the per-category <see cref="DeviceInventory.All"/> / <see cref="DeviceInventory.Primary"/>
/// accessors, multi-disk enumeration + ordering, and the static display identity (captions/specs).</summary>
public class DeviceInventoryTests {
    private static readonly CpuStaticInfo Cpu = new("Intel Core i9-14900K", 24, 32, 3200);
    private static readonly MemoryStaticInfo Memory = new(32, "DDR5", 6000, 2);
    private static readonly GpuStaticInfo Gpu = new("NVIDIA GeForce RTX 4080");

    private static PhysicalDiskInfo Disk(int id, string model = "Test Disk", ulong sizeBytes = 0) =>
        new(id, model, "NVMe SSD", sizeBytes, true);

    private static VolumeInfo Vol(int disk, char letter, string label = "") =>
        new(disk, letter, label, "NTFS", 1000, 500);

    private static DeviceInventory Compose(
        PhysicalDiskInfo[] disks, VolumeInfo[] volumes, string netName = "Ethernet", string netSpec = "Intel I225-V") =>
        DeviceInventory.Compose(Cpu, Memory, Gpu, disks, volumes, netName, netSpec);

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
        Assert.Equal("GeForce RTX 4080", gpu.Sub);     // vendor prefix trimmed
        Assert.Equal("NVIDIA GeForce RTX 4080", gpu.Spec);

        var net = inv.Primary(DeviceCategory.Network)!;
        Assert.Equal("Wi-Fi", net.Name);
        Assert.Equal("Intel AX211", net.Spec);
    }
}
