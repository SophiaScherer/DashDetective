using DashDetective.Services.Diagnostics;
using DashDetective.Services.Platform.Linux;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DashDetective.Tabs.Hardware;

/// <summary>
/// Drive facts, one row per physical disk, from the shared <see cref="SysBlockFacts"/> derivation — the
/// same one the Storage tab's cards read, so the two tabs cannot name or size the same drive differently.
/// Each keeps its own wording: "NVMe" on this spec row, "NVMe SSD" on the Storage card.
///
/// <b>Health is permanently "—".</b> The Windows arm folds <c>MSFT_PhysicalDisk</c>'s <c>HealthStatus</c>
/// into Good/Warning/Unhealthy; the Linux equivalent is SMART, which needs root. Nothing rootless answers
/// the same question, so the row stays blank rather than reporting a near-miss.
///
/// Never throws: any failure yields <see cref="StorageInfo.Unknown"/>.
/// </summary>
internal sealed class LinuxStorageInfoProvider : IStorageInfoProvider {
    private readonly IProcFileSystem _proc;

    public LinuxStorageInfoProvider() : this(new ProcFileSystem()) { }

    /// <summary>Test seam: injects the filesystem so the card can be exercised against canned fixtures
    /// from any dev machine.</summary>
    internal LinuxStorageInfoProvider(IProcFileSystem proc) => _proc = proc;

    public Task<StorageInfo> GetAsync() => Task.Run(Read);

    private StorageInfo Read() {
        try {
            var facts = SysBlockFacts.Read(_proc);
            if (facts.Disks.Count == 0)
                return StorageInfo.Unknown;

            var devices = new List<StorageDeviceInfo>(facts.Disks.Count);
            ulong totalBytes = 0;

            foreach (var disk in facts.Disks) {
                totalBytes += disk.SizeBytes;
                devices.Add(new StorageDeviceInfo(
                    string.IsNullOrWhiteSpace(disk.Model) ? "Drive" : disk.Model,
                    StorageSpecFormatter.DriveDetail(disk.SizeBytes, StorageSpecFormatter.TypeLabel(disk.Kind))));
            }

            return new StorageInfo(
                Summary: StorageSpecFormatter.Summary(devices.Count, totalBytes),
                Drives: devices,
                TotalHealth: "—");
        } catch (Exception e) {
            Log.Warn("LinuxStorageInfoProvider read failed", e);
            return StorageInfo.Unknown;
        }
    }
}
