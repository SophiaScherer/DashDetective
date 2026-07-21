using DashDetective.Services.SystemMetrics;
using DashDetective.Tabs.Storage;
using Xunit;

namespace DashDetective.Tests.Tabs.Storage;

/// <summary>Covers <see cref="StorageComposer.Compose"/>: the per-disk used/free/usage rollup across a
/// disk's volumes, the volume→disk join, the card name (lowest-lettered volume, "Local Disk" when
/// unlabelled, disk model when unlettered), health folding, and disk ordering.</summary>
public class StorageComposerTests {
    private static PhysicalDiskInfo Disk(int id, string model = "Test Disk", bool healthy = true) =>
        new(id, model, "", 0, healthy);

    private static VolumeInfo Vol(int? disk, char? letter, ulong size, ulong free,
        string label = "", string fs = "NTFS") =>
        new(disk, letter, label, fs, size, free);

    [Fact]
    public void Compose_SingleVolume_RollsUpUsedFreeAndUsage() {
        var cards = StorageComposer.Compose(
            new[] { Disk(0) },
            new[] { Vol(0, 'C', size: 1000, free: 250) });

        var card = Assert.Single(cards);
        Assert.Equal(750, card.UsedBytes);
        Assert.Equal(250, card.FreeBytes);
        Assert.Equal(75, card.UsagePercent);
    }

    [Fact]
    public void Compose_MultipleVolumesOnDisk_SumsAcrossThem() {
        var cards = StorageComposer.Compose(
            new[] { Disk(0) },
            new[] { Vol(0, 'C', 1000, 250), Vol(0, null, 500, 100) });

        var card = Assert.Single(cards);
        Assert.Equal(1150, card.UsedBytes);   // (1000-250) + (500-100)
        Assert.Equal(350, card.FreeBytes);
        Assert.Equal(76.67, card.UsagePercent, 2);   // 1150 / 1500
    }

    [Fact]
    public void Compose_OnlyCountsVolumesOnThatDisk() {
        var cards = StorageComposer.Compose(
            new[] { Disk(0) },
            new[] { Vol(0, 'C', 1000, 250), Vol(1, 'D', 9000, 9000) });

        var card = Assert.Single(cards);
        Assert.Equal(750, card.UsedBytes);
        Assert.Equal(250, card.FreeBytes);
    }

    [Fact]
    public void Compose_DiskWithNoVolumes_IsZeroUsage() {
        var cards = StorageComposer.Compose(new[] { Disk(0, "Empty Disk") }, new VolumeInfo[0]);

        var card = Assert.Single(cards);
        Assert.Equal(0, card.UsedBytes);
        Assert.Equal(0, card.FreeBytes);
        Assert.Equal(0, card.UsagePercent);
        Assert.Equal("Empty Disk", card.Name);   // no lettered volume → falls back to the model
    }

    [Fact]
    public void Compose_UnlabelledLetteredVolume_NamesLocalDisk() {
        var cards = StorageComposer.Compose(new[] { Disk(0) }, new[] { Vol(0, 'C', 1000, 500) });
        Assert.Equal("Local Disk (C:)", Assert.Single(cards).Name);
    }

    [Fact]
    public void Compose_LabelledVolume_UsesLabelInName() {
        var cards = StorageComposer.Compose(
            new[] { Disk(0) }, new[] { Vol(0, 'C', 1000, 500, label: "Windows") });
        Assert.Equal("Windows (C:)", Assert.Single(cards).Name);
    }

    [Fact]
    public void Compose_MultipleLetteredVolumes_NamesFromLowestLetter() {
        var cards = StorageComposer.Compose(
            new[] { Disk(0) },
            new[] { Vol(0, 'D', 1000, 500, label: "Data"), Vol(0, 'C', 1000, 500, label: "Windows") });
        Assert.Equal("Windows (C:)", Assert.Single(cards).Name);
    }

    [Fact]
    public void Compose_UnhealthyDisk_MapsToCaution() {
        var cards = StorageComposer.Compose(
            new[] { Disk(0, healthy: false) }, new[] { Vol(0, 'C', 1000, 500) });
        Assert.Equal(DriveHealth.Caution, Assert.Single(cards).Health);
    }

    [Fact]
    public void Compose_HealthyDisk_MapsToHealthy() {
        var cards = StorageComposer.Compose(new[] { Disk(0) }, new[] { Vol(0, 'C', 1000, 500) });
        Assert.Equal(DriveHealth.Healthy, Assert.Single(cards).Health);
    }

    [Fact]
    public void Compose_OrdersCardsByDiskNumber() {
        var cards = StorageComposer.Compose(
            new[] { Disk(2), Disk(0), Disk(1) },
            new VolumeInfo[0]);

        Assert.Equal(3, cards.Count);
        Assert.Equal(0, cards[0].DiskNumber);
        Assert.Equal(1, cards[1].DiskNumber);
        Assert.Equal(2, cards[2].DiskNumber);
    }
}
