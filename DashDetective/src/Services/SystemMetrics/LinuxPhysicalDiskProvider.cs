using DashDetective.Services.Diagnostics;
using DashDetective.Services.Platform.Linux;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DashDetective.Services.SystemMetrics;

/// <summary>
/// Enumerates the machine's physical disks from <c>/sys/block</c> for the Storage tab's summary cards —
/// model, capacity and media kind — over the shared <see cref="SysBlockFacts"/> derivation, so this and the
/// Hardware tab's Storage Devices card cannot disagree about the same drives. Loop, ram and optical devices
/// are filtered there; without that a stock Ubuntu GNOME install would render ~25 snap cards.
///
/// <b>Health is always healthy and temperature always <c>null</c>.</b> Both need SMART, which is not
/// readable without root, and neither has a rootless near-miss worth substituting. The temperature provider
/// is taken by constructor exactly as the Windows arm takes it, so the GPU/temperature milestone is one line
/// in <c>HardwareProviders.Linux()</c>.
///
/// Stateless and never throws: any failure yields an empty list.
/// </summary>
internal sealed class LinuxPhysicalDiskProvider : IPhysicalDiskProvider {
    private readonly IDiskTemperatureProvider _temperature;
    private readonly IProcFileSystem _proc;

    public LinuxPhysicalDiskProvider(IDiskTemperatureProvider temperature)
        : this(temperature, new ProcFileSystem()) { }

    /// <summary>Test seam: injects the filesystem so the <c>/sys/block</c> walk and its filters can be
    /// exercised against canned fixtures from any dev machine.</summary>
    internal LinuxPhysicalDiskProvider(IDiskTemperatureProvider temperature, IProcFileSystem proc) {
        _temperature = temperature;
        _proc = proc;
    }

    public Task<IReadOnlyList<PhysicalDiskInfo>> GetAsync() => Task.Run(Read);

    private IReadOnlyList<PhysicalDiskInfo> Read() {
        try {
            var facts = SysBlockFacts.Read(_proc);
            var disks = new List<PhysicalDiskInfo>(facts.Disks.Count);

            foreach (var disk in facts.Disks) {
                // Only NVMe drives have a readable composite temperature, matching the Windows arm; until
                // the temperature milestone lands, the provider behind this returns null.
                var celsius = disk.Kind == DriveKind.Nvme ? _temperature.ReadCelsius(disk.DiskNumber) : null;

                disks.Add(new PhysicalDiskInfo(
                    disk.DiskNumber,
                    string.IsNullOrEmpty(disk.Model) ? "Drive" : disk.Model,
                    DriveKinds.CardLabel(disk.Kind),
                    disk.SizeBytes,
                    IsHealthy: true,
                    celsius));
            }

            return disks;
        } catch (Exception e) {
            Log.Warn("LinuxPhysicalDiskProvider read failed", e);
            return Array.Empty<PhysicalDiskInfo>();
        }
    }
}
