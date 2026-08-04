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

    /// <summary>The real readers on Windows, or the "nothing to report" set anywhere else — which returns
    /// exactly the <c>.Unknown</c> / empty / <c>null</c> values the old inline
    /// <c>OperatingSystem.IsWindows()</c> guards returned, so a non-Windows host renders "—" rather than
    /// failing. This is the single place the platform is decided for all seven.</summary>
    public static HardwareProviders ForCurrentPlatform() {
        if (OperatingSystem.IsWindows())
            return Windows();

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
}
