using DashDetective.Services.SystemMetrics;
using System;

namespace DashDetective.Tabs.Performance;

/// <summary>One GPU's sensor reading. Each value is independently nullable: a driver may report temperature
/// but not power (or neither), and the two tiles blank separately rather than together.</summary>
internal readonly record struct GpuSensorSample(double? TemperatureCelsius, double? PowerWatts) {
    /// <summary>A reading with nothing reported — what every soft-failure path returns.</summary>
    public static GpuSensorSample None => new(null, null);
}

/// <summary>
/// Reads temperature/power for one GPU vendor's adapters. One implementation per vendor SDK (NVAPI+NVML for
/// NVIDIA; AMD and Intel are deferred), kept isolated from one another so a vendor can be added or dropped
/// without touching the others.
///
/// Implementations must <b>never throw</b>: an absent DLL, an old driver, an unsupported adapter or a failed
/// call all soft-fail to <see cref="GpuSensorSample.None"/>, matching every other provider in this codebase.
/// </summary>
internal interface IGpuSensorReader : IDisposable {
    /// <summary>The PCI vendor id this reader serves (e.g. <c>0x10DE</c> for NVIDIA).</summary>
    uint VendorId { get; }

    /// <summary>Reads the adapter with the given PCI identity, or <see cref="GpuSensorSample.None"/> when this
    /// reader can't see it.</summary>
    GpuSensorSample Read(GpuPciId pci);
}
