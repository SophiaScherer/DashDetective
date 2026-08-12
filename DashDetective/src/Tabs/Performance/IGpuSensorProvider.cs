using DashDetective.Services.SystemMetrics;
using System;

namespace DashDetective.Tabs.Performance;

/// <summary>
/// A source of one GPU's temperature and power. Drives the Performance tab's per-GPU Temp / Power tiles;
/// the page owns a single instance and reads every adapter through it each throughput tick.
///
/// Where utilisation has an in-box API on both platforms, thermals do not: Windows has none at all and is
/// served by whichever vendor SDK the display driver installed, while Linux publishes them in sysfs for
/// the open drivers and nowhere for the proprietary one. Implementations must never throw — an absent
/// library, an old driver or an unsupported adapter all soft-fail to <see cref="GpuSensorSample.None"/>,
/// which leaves both tiles at "—".
/// </summary>
internal interface IGpuSensorProvider : IDisposable {
    /// <summary>Reads one adapter's sensors, or <see cref="GpuSensorSample.None"/> when nothing can report
    /// them. <paramref name="adapterKey"/> is the adapter token the enumeration assigned — a LUID on
    /// Windows, a PCI address on Linux.</summary>
    GpuSensorSample Read(string adapterKey, GpuPciId? pci);

    /// <summary>The reader for this machine — the only place the platform is decided for this seam.</summary>
    static IGpuSensorProvider ForCurrentPlatform() {
        if (OperatingSystem.IsWindows())
            return new WindowsGpuSensorProvider();

        return new UnsupportedGpuSensorProvider();
    }
}

/// <summary>The no-sensors provider — what a platform with no thermal source gets. Both tiles keep the "—"
/// they were built with, which is what the Performance tab already renders for an adapter whose vendor has
/// no reader.</summary>
internal sealed class UnsupportedGpuSensorProvider : IGpuSensorProvider {
    public GpuSensorSample Read(string adapterKey, GpuPciId? pci) => GpuSensorSample.None;

    public void Dispose() { }
}
