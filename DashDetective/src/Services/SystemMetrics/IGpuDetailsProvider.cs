using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace DashDetective.Services.SystemMetrics;

/// <summary>The static facts Task Manager lists beside an adapter's graphs. Each is null where the platform
/// does not publish it, so the Performance tab omits that row rather than showing a stand-in.</summary>
/// <param name="DriverVersion">The display driver's version, e.g. "32.0.16.1062".</param>
/// <param name="DirectXVersion">The Direct3D runtime and the adapter's highest feature level, e.g.
/// "12 (FL 12.1)" (see <see cref="FormatDirectX"/>).</param>
/// <param name="PhysicalLocation">Where the adapter sits on the PCI bus, e.g. "PCI bus 1, device 0,
/// function 0" (see <see cref="PciLocation"/>).</param>
public sealed record GpuDetails(string? DriverVersion, string? DirectXVersion, string? PhysicalLocation) {
    /// <summary>Formats a <c>D3D_FEATURE_LEVEL</c> value (0xc100 is 12_1) as "12 (FL 12.1)", Task Manager's
    /// notation. The runtime is always 12: the level was found by asking Direct3D 12 for it.</summary>
    public static string FormatDirectX(int featureLevel) =>
        string.Create(CultureInfo.InvariantCulture,
                      $"12 (FL {(featureLevel >> 12) & 0xF}.{(featureLevel >> 8) & 0xF})");
}

/// <summary>
/// Reads each GPU's <see cref="GpuDetails"/>, by the same adapter key the inventory uses. Separate from
/// <see cref="IGpuAdapterProvider"/> on purpose: on Windows the DirectX version loads Direct3D 12 and the
/// vendor's user-mode driver into the process (and can wake a sleeping discrete GPU), so only the
/// Performance tab asks, once its GPU rows exist — never the Dashboard, Storage or the alert watcher.
///
/// Implementations must never throw; an adapter they know nothing about is absent from the result.
/// </summary>
internal interface IGpuDetailsProvider {
    Task<IReadOnlyDictionary<string, GpuDetails>> GetAsync(IReadOnlyCollection<string> adapterKeys);

    /// <summary>The process-wide reader for this machine, cached per adapter: the facts are static, so each
    /// adapter is read once per run however many refreshes ask. The only place the platform is decided.</summary>
    static IGpuDetailsProvider ForCurrentPlatform() => Shared.Value;

    private static readonly Lazy<IGpuDetailsProvider> Shared = new(() => new GpuDetailsCache(PlatformReader()));

    private static IGpuDetailsProvider PlatformReader() {
        if (OperatingSystem.IsWindows())
            return new WindowsGpuDetailsProvider();

        if (OperatingSystem.IsLinux())
            return new LinuxGpuDetailsProvider();

        return new UnsupportedGpuDetailsProvider();
    }
}
