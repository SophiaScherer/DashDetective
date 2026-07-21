using DashDetective.Services.SystemMetrics;
using System.Collections.Generic;
using System.Linq;

namespace DashDetective.Tabs.Storage;

/// <summary>A drive's reported health, reduced to the two states the summary card shows.</summary>
public enum DriveHealth { Healthy, Caution }

/// <summary>
/// The composed data for one drive summary card: display name, model, health, and the used-space rollup
/// (usage %, used and free bytes) summed across the disk's volumes. Pure data — the view model formats the
/// bytes and picks brushes.
/// </summary>
public readonly record struct DriveCardData(
    int DiskNumber, string Name, string Model, DriveHealth Health,
    double UsagePercent, long UsedBytes, long FreeBytes);

/// <summary>
/// Joins physical disks (<see cref="PhysicalDiskInfo"/>) with their volumes (<see cref="VolumeInfo"/>, keyed
/// by disk number) into one <see cref="DriveCardData"/> per disk: capacity/used/free are summed across the
/// disk's volumes (matching what Explorer shows, not the raw platter size), the name comes from the disk's
/// lowest-lettered volume, and health folds <c>HealthStatus</c> into Healthy/Caution. Pure and
/// side-effect-free — no WMI, IO or UI — so it is unit-tested directly.
/// </summary>
public static class StorageComposer {
    public static IReadOnlyList<DriveCardData> Compose(
        IReadOnlyList<PhysicalDiskInfo> disks, IReadOnlyList<VolumeInfo> volumes) {
        var cards = new List<DriveCardData>();

        foreach (var disk in disks.OrderBy(d => d.DeviceId)) {
            var diskVolumes = volumes.Where(v => v.DiskNumber == disk.DeviceId).ToList();

            ulong totalSize = 0, totalFree = 0;
            foreach (var volume in diskVolumes) {
                totalSize += volume.SizeBytes;
                totalFree += volume.FreeBytes;
            }
            var used = totalSize > totalFree ? totalSize - totalFree : 0;
            var usagePercent = totalSize > 0 ? used / (double)totalSize * 100 : 0;

            cards.Add(new DriveCardData(
                disk.DeviceId,
                DriveName(disk, diskVolumes),
                disk.Model,
                disk.IsHealthy ? DriveHealth.Healthy : DriveHealth.Caution,
                usagePercent,
                (long)used,
                (long)totalFree));
        }

        return cards;
    }

    /// <summary>The card title: the label + letter of the disk's lowest-lettered volume (an unlabelled
    /// volume reads "Local Disk", like Windows), or the disk model when the disk has no lettered volume.</summary>
    private static string DriveName(PhysicalDiskInfo disk, IReadOnlyList<VolumeInfo> diskVolumes) {
        var primary = diskVolumes
            .Where(v => v.DriveLetter.HasValue)
            .OrderBy(v => v.DriveLetter)
            .FirstOrDefault();

        if (primary.DriveLetter is { } letter) {
            var label = string.IsNullOrEmpty(primary.Label) ? "Local Disk" : primary.Label;
            return $"{label} ({letter}:)";
        }

        return disk.Model;
    }
}
