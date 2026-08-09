using DashDetective.Services.SystemMetrics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DashDetective.Tabs.Storage;

/// <summary>A drive's reported health, reduced to the two states the summary card shows.</summary>
public enum DriveHealth { Healthy, Caution }

/// <summary>
/// The composed data for one drive summary card: display name, model, health, the used-space rollup
/// (usage %, used and free bytes) summed across the disk's volumes, and the disk's temperature in °C when
/// available (NVMe only; <c>null</c> otherwise). Pure data — the view model formats the bytes and picks brushes.
/// </summary>
public readonly record struct DriveCardData(
    int DiskNumber, string Name, string Model, DriveHealth Health,
    double UsagePercent, long UsedBytes, long FreeBytes, double? TemperatureCelsius = null);

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
                (long)totalFree,
                disk.TemperatureCelsius));
        }

        return cards;
    }

    /// <summary>The card title: the label plus the name of the disk's primary volume — its lowest-lettered
    /// one on Windows, its shallowest mount point on Linux ("Ubuntu (/)" beats "… (/boot/efi)"). An
    /// unlabelled volume reads "Local Disk", like Windows. Falls back to the disk model when the disk hosts
    /// no mounted volume at all.</summary>
    private static string DriveName(PhysicalDiskInfo disk, IReadOnlyList<VolumeInfo> diskVolumes) {
        var lettered = diskVolumes
            .Where(v => v.DriveLetter.HasValue)
            .OrderBy(v => v.DriveLetter)
            .FirstOrDefault();

        if (lettered.DriveLetter is { } letter)
            return Titled(lettered, $"{letter}:");

        // A record struct's default has null strings, so the "no mounted volume" case is tested for
        // emptiness rather than by reading through the result of FirstOrDefault.
        var mounted = diskVolumes
            .Where(v => !string.IsNullOrEmpty(v.MountPoint))
            .OrderBy(v => v.MountPoint.Length)
            .ThenBy(v => v.MountPoint, StringComparer.Ordinal)
            .FirstOrDefault();

        return string.IsNullOrEmpty(mounted.MountPoint) ? disk.Model : Titled(mounted, mounted.MountPoint);
    }

    /// <summary>"&lt;label&gt; (&lt;where&gt;)", with Explorer's "Local Disk" standing in for a volume that
    /// carries no label.</summary>
    private static string Titled(VolumeInfo volume, string where) =>
        $"{(string.IsNullOrEmpty(volume.Label) ? "Local Disk" : volume.Label)} ({where})";
}
