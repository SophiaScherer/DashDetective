using System;
using System.Collections.Generic;

namespace DashDetective.Services.SystemMetrics;

/// <summary>
/// A source of per-physical-GPU utilisation, one reading per adapter. Drives the Dashboard's GPU cards and
/// the Performance tab's GPU rows, and tells <see cref="DeviceInventory"/> which adapters are actually
/// backed by a live driver — each consumer owns its own instance and its own timer.
///
/// <b>Its keys must match <see cref="IGpuAdapterProvider"/>'s.</b> The inventory intersects the two, so an
/// implementation that derives the adapter key differently from its enumeration counterpart yields no GPU
/// at all rather than a wrong one — a silent failure, since every individual reading looks fine. Both
/// arms take the key from one shared derivation for exactly that reason.
///
/// Unlike the <c>HardwareProviders</c> members this is <b>stateful</b>: an implementation may report the
/// interval since the previous call, so an instance may not be shared between pages. Implementations must
/// never throw: any failure yields an empty map, and one that has gone inert yields one forever.
/// </summary>
internal interface IGpuUsageSampler : IDisposable {
    /// <summary>Returns one reading per physical GPU at the moment of the call, keyed by adapter token.
    /// Empty on any failure; an adapter the platform cannot report utilisation for is still named, with a
    /// null <see cref="GpuAdapterSample.Overall"/>.</summary>
    IReadOnlyDictionary<string, GpuAdapterSample> SampleAdapters();

    /// <summary>
    /// Whether the user has opted into readings that cost a process spawn. Defaults to ignoring the flag,
    /// because only one arm has such a source: Linux NVIDIA utilisation exists solely through
    /// <c>nvidia-smi</c>, while PDH and sysfs are cheap in-process reads that need no opt-in. A default
    /// implementation rather than a property on every arm keeps the cost of the setting on the one
    /// platform that has it.
    /// </summary>
    bool NvidiaMetricsEnabled {
        get => false;
        set { }
    }

    /// <summary>The sampler for this machine — the only place the platform is decided for this seam.</summary>
    static IGpuUsageSampler ForCurrentPlatform() {
        if (OperatingSystem.IsWindows())
            return new WindowsGpuUsageSampler();

        if (OperatingSystem.IsLinux())
            return new LinuxGpuUsageSampler();

        return new UnsupportedGpuUsageSampler();
    }
}
