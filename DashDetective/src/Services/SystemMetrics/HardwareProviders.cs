using DashDetective.Tabs.Dashboard;
using System;
using System.Runtime.Versioning;

namespace DashDetective.Services.SystemMetrics;

/// <summary>
/// The "what hardware is in this machine" reads, resolved once per consuming page — the
/// <c>MetricSamplers</c> shape. <see cref="DeviceInventory"/> is the natural centre of this set: its whole
/// job is that question, and it fires five of these members together in one <c>Task.WhenAll</c>.
///
/// <b>Every member must be stateless.</b> <see cref="ForCurrentPlatform"/> is called once per consuming
/// view model (Dashboard, Performance, Storage), so three instances exist, and <c>DeviceInventory</c> runs
/// them concurrently. A provider that caches across calls does not belong here — the stateful ones
/// (the Network tab's PID→name cache, the process snapshot's previous-CPU tables, the GPU interops' init
/// handles) each have exactly one consumer and are deliberately kept out.
/// </summary>
internal sealed record HardwareProviders(
    ICpuInfoProvider Cpu,
    IMemoryInfoProvider Memory,
    ISystemInfoProvider System,
    IGpuAdapterProvider GpuAdapters,
    IPhysicalDiskProvider Disks,
    IVolumeProvider Volumes,
    IDiskTemperatureProvider DiskTemperature) {

    /// <summary>The real readers for this machine, or the "nothing to report" set on a platform with no
    /// implementation — which returns exactly the <c>.Unknown</c> / empty / <c>null</c> values the old
    /// inline <c>OperatingSystem.IsWindows()</c> guards returned, so an unsupported host renders "—"
    /// rather than failing. This is the single place the platform is decided for all seven.</summary>
    public static HardwareProviders ForCurrentPlatform() {
        if (OperatingSystem.IsWindows())
            return Windows();

        if (OperatingSystem.IsLinux())
            return Linux();

        return new HardwareProviders(
            new UnsupportedCpuInfoProvider(),
            new UnsupportedMemoryInfoProvider(),
            new UnsupportedSystemInfoProvider(),
            new UnsupportedGpuAdapterProvider(),
            new UnsupportedPhysicalDiskProvider(),
            new UnsupportedVolumeProvider(),
            new UnsupportedDiskTemperatureProvider());
    }

    [SupportedOSPlatform("windows")]
    private static HardwareProviders Windows() {
        // One temperature reader, shared by the Storage page and the disk enumeration that stamps each
        // NVMe card — so "the drive's temperature" has a single source rather than two objects that
        // happen to agree.
        var temperature = new WindowsDiskTemperatureProvider();

        return new HardwareProviders(
            new WindowsCpuInfoProvider(),
            new WindowsMemoryInfoProvider(),
            new WindowsSystemInfoProvider(),
            new WindowsGpuAdapterProvider(),
            new WindowsPhysicalDiskProvider(temperature),
            new WindowsVolumeProvider(),
            temperature);
    }

    /// <summary>
    /// The <c>/proc</c> and <c>/sys</c> readers, filled in one milestone at a time — a member with no Linux
    /// implementation yet keeps its <c>Unsupported*</c> instance and renders "—", which is the whole point
    /// of the degrade-first port. Disk temperature arrives later in the GPU milestone; per-DIMM memory
    /// facts need <c>dmidecode</c> with root and stay unsupported for good.
    ///
    /// Unlike <see cref="Windows"/> this carries no <see cref="SupportedOSPlatformAttribute"/>: the Linux
    /// readers are portable managed code over <c>IProcFileSystem</c>, so there is no annotated API for
    /// CA1416 to see and the attribute would be decoration rather than enforcement.
    /// </summary>
    private static HardwareProviders Linux() {
        // The disk provider takes the temperature reader the way the Windows arm does, so the milestone
        // that lands a real one is a single swap here.
        var temperature = new UnsupportedDiskTemperatureProvider();

        return new HardwareProviders(
            new LinuxCpuInfoProvider(),
            new UnsupportedMemoryInfoProvider(),
            new LinuxSystemInfoProvider(),
            new LinuxGpuAdapterProvider(),
            new LinuxPhysicalDiskProvider(temperature),
            new LinuxVolumeProvider(),
            temperature);
    }
}
