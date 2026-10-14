using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace DashDetective.Services.SystemMetrics;

/// <summary>An adapter's PCI identity, exactly as <c>DXGI_ADAPTER_DESC1</c> reports it. The vendor sensor
/// SDKs (NVAPI, NVML) report the same three ids, so this is what a sensor reading is attributed by;
/// <see cref="VendorId"/> also selects which vendor reader — if any — applies to the adapter.</summary>
public sealed record GpuPciId(uint VendorId, uint DeviceId, uint SubSysId, uint Revision);

/// <summary>The static facts Task Manager lists beside an adapter's graphs. Each is null where the platform
/// does not publish it, so the Performance tab omits that row rather than showing a stand-in.</summary>
/// <param name="DriverVersion">The display driver's version, e.g. "32.0.16.1062".</param>
/// <param name="DirectXVersion">The Direct3D runtime and the adapter's highest feature level, e.g.
/// "12 (FL 12.1)" (see <see cref="FormatDirectX"/>).</param>
/// <param name="PhysicalLocation">Where the adapter sits on the PCI bus, e.g. "PCI bus 1, device 0,
/// function 0" (see <see cref="PciLocation"/>).</param>
public sealed record GpuDetails(string? DriverVersion, string? DirectXVersion, string? PhysicalLocation) {
    /// <summary>Formats a <c>D3D_FEATURE_LEVEL</c> value (0xc100 is 12_1) as Task Manager words it,
    /// "12 (FL 12.1)". The runtime is always 12: the level was found by asking Direct3D 12 for it.</summary>
    public static string FormatDirectX(int featureLevel) =>
        string.Create(CultureInfo.InvariantCulture,
                      $"12 (FL {(featureLevel >> 12) & 0xF}.{(featureLevel >> 8) & 0xF})");
}

/// <summary>One graphics adapter: <see cref="LuidToken"/>, the platform's own stable identity for it, the
/// friendly <see cref="Name"/>, whether it is a placeholder rather than a real adapter
/// (<see cref="IsSoftware"/>, to be filtered out), its dedicated VRAM in bytes (carried onto
/// <see cref="DeviceInstance.VramBytes"/> for the Performance tab's GPU VRAM tile), its
/// <see cref="Pci"/> identity (which vendor sensor readings join on), and its shared system memory in bytes
/// (<see cref="SharedSystemMemory"/>, null where the platform publishes none), and the driver, DirectX and
/// bus-location facts in <see cref="Details"/>.
///
/// <b><see cref="LuidToken"/> is named for Windows but is not always a LUID.</b> It is whatever this
/// platform uses to say "the same adapter" across independent readers: the PDH-style
/// <c>luid_0x…_0x…</c> token that joins to the <c>\GPU Engine(*)</c> counter instances on Windows, and the
/// card's PCI address on Linux, which has no LUID. What matters is that the enumeration and the
/// utilisation sampler derive it identically — the inventory intersects the two, so a mismatch yields no
/// GPU at all rather than a wrong one.</summary>
public sealed record GpuAdapter(
    string LuidToken, string Name, bool IsSoftware, ulong DedicatedVideoMemory, GpuPciId? Pci = null,
    ulong? SharedSystemMemory = null, GpuDetails? Details = null) {
    /// <summary>Formats a LUID's high/low parts into the PDH instance-name token
    /// (<c>luid_0x00000000_0x0000e54b</c>) — lowercase, eight hex digits each — so it joins directly
    /// against the <c>\GPU Engine(*)</c> counter instances. Pure; unit-tested. Lives on the adapter model
    /// rather than the DXGI reader because the token format is a property of the adapter, not of any one
    /// platform's way of enumerating it.</summary>
    public static string FormatLuidToken(int high, uint low) =>
        string.Create(CultureInfo.InvariantCulture, $"luid_0x{(uint)high:x8}_0x{low:x8}");
}

/// <summary>Enumerates the machine's graphics adapters — the authoritative LUID→name map behind
/// multi-GPU. Implementations must never throw: any failure yields an empty list.</summary>
internal interface IGpuAdapterProvider {
    Task<IReadOnlyList<GpuAdapter>> GetAsync();
}
