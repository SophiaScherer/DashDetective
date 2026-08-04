using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace DashDetective.Services.SystemMetrics;

/// <summary>An adapter's PCI identity, exactly as <c>DXGI_ADAPTER_DESC1</c> reports it. The vendor sensor
/// SDKs (NVAPI, NVML) report the same three ids, so this is what a sensor reading is attributed by;
/// <see cref="VendorId"/> also selects which vendor reader — if any — applies to the adapter.</summary>
public sealed record GpuPciId(uint VendorId, uint DeviceId, uint SubSysId, uint Revision);

/// <summary>One graphics adapter as DXGI reports it: the PDH-style <see cref="LuidToken"/> that joins to
/// the <c>\GPU Engine(*)</c> counter instances, the friendly <see cref="Name"/> (DXGI's description),
/// whether it is a software/basic-render adapter (<see cref="IsSoftware"/>, to be filtered out), its
/// dedicated VRAM in bytes (carried onto <see cref="DeviceInstance.VramBytes"/> for the Performance tab's
/// GPU VRAM tile), and its <see cref="Pci"/> identity (which vendor sensor readings join on).</summary>
public sealed record GpuAdapter(
    string LuidToken, string Name, bool IsSoftware, ulong DedicatedVideoMemory, GpuPciId? Pci = null) {
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
