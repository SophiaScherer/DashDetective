using DashDetective.Services.SystemMetrics;
using System.Collections.Generic;

namespace DashDetective.Tabs.Performance;

/// <summary>One adapter's PCI identity as a vendor SDK reports it. Distinct from
/// <see cref="GpuPciId"/> because NVAPI and NVML report the device id already packed
/// (<c>(device &lt;&lt; 16) | vendor</c>), where DXGI keeps vendor and device separate.</summary>
internal readonly record struct VendorPciId(uint PackedDeviceId, uint SubSysId, uint Revision);

/// <summary>
/// Joins a DXGI adapter to the same adapter as a vendor SDK enumerates it. NVAPI and NVML have no LUID, but
/// both report the PCI device/subsystem/revision ids that <c>DXGI_ADAPTER_DESC1</c> also carries — so the
/// join is exact rather than positional. Pure; unit-tested.
/// </summary>
internal static class GpuPciMatcher {
    /// <summary>Packs a vendor + device id the way NVAPI's <c>GetPCIIdentifiers</c> and NVML's
    /// <c>pciDeviceId</c> report it: <c>(device &lt;&lt; 16) | vendor</c>. An RTX 3060 (vendor 0x10DE,
    /// device 0x2504) packs to 0x250410DE.</summary>
    internal static uint PackDeviceId(uint vendorId, uint deviceId) =>
        (deviceId << 16) | (vendorId & 0xFFFF);

    /// <summary>Index of the vendor-reported adapter matching <paramref name="target"/>, or <c>null</c> when
    /// none does. Indices already in <paramref name="claimed"/> are skipped, so two identical cards pair up in
    /// enumeration order instead of both resolving to the first.</summary>
    internal static int? Match(GpuPciId target, IReadOnlyList<VendorPciId> candidates,
                               IReadOnlySet<int>? claimed = null) {
        var wanted = PackDeviceId(target.VendorId, target.DeviceId);
        for (var i = 0; i < candidates.Count; i++) {
            if (claimed is not null && claimed.Contains(i))
                continue;

            var candidate = candidates[i];
            if (candidate.PackedDeviceId != wanted)
                continue;
            // Subsystem and revision are only compared when the vendor actually reported them, so a driver
            // that leaves either at zero still matches on the device id rather than reporting no sensors.
            if (candidate.SubSysId != 0 && candidate.SubSysId != target.SubSysId)
                continue;
            if (candidate.Revision != 0 && candidate.Revision != target.Revision)
                continue;

            return i;
        }
        return null;
    }
}
