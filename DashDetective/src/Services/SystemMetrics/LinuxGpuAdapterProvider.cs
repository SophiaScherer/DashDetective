using DashDetective.Services.Diagnostics;
using DashDetective.Services.Platform.Linux;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DashDetective.Services.SystemMetrics;

/// <summary>
/// Enumerates the machine's graphics adapters from <c>/sys/class/drm</c> over the shared
/// <see cref="DrmCardFacts"/> derivation — the Linux counterpart to
/// <see cref="WindowsGpuAdapterProvider"/>'s DXGI walk, and the authoritative adapter list behind
/// multi-GPU.
///
/// <b><see cref="GpuAdapter.LuidToken"/> carries the card's PCI address here, not a LUID.</b> Linux has no
/// LUID, and the inventory only builds a card for an adapter that this enumeration and the utilisation
/// sampler <i>both</i> report — so the two must derive the same key from the same authority or the GPU
/// section silently comes up empty. <see cref="DrmCardFacts.Key"/> is that single authority; this provider
/// does not compute one of its own.
///
/// Stateless and never throws: any failure yields an empty list.
/// </summary>
internal sealed class LinuxGpuAdapterProvider : IGpuAdapterProvider {
    private readonly IProcFileSystem _proc;

    public LinuxGpuAdapterProvider() : this(new ProcFileSystem()) { }

    /// <summary>Test seam: injects the filesystem so the <c>/sys/class/drm</c> walk can be exercised
    /// against canned fixtures from any dev machine.</summary>
    internal LinuxGpuAdapterProvider(IProcFileSystem proc) => _proc = proc;

    public Task<IReadOnlyList<GpuAdapter>> GetAsync() => Task.Run(Read);

    private IReadOnlyList<GpuAdapter> Read() {
        try {
            // Named, not the bare walk: this list is what labels the Dashboard and Performance GPU cards.
            var cards = DrmCardFacts.ReadNamed(_proc);
            var adapters = new List<GpuAdapter>(cards.Count);

            foreach (var card in cards)
                adapters.Add(new GpuAdapter(
                    card.Key,
                    card.AdapterName,
                    card.IsSoftware,
                    card.VramBytes,
                    new GpuPciId(card.VendorId, card.DeviceId, PackSubSysId(card), card.Revision)));

            return adapters;
        } catch (Exception e) {
            Log.Warn("LinuxGpuAdapterProvider read failed", e);
            return [];
        }
    }

    /// <summary>Packs the subsystem ids the way PCI config space — and therefore
    /// <c>DXGI_ADAPTER_DESC1.SubSysId</c> — carries them: device in the high half, vendor in the low. sysfs
    /// splits them into two files, so leaving either out would make the same card read differently on the
    /// two platforms.</summary>
    private static uint PackSubSysId(DrmCardFacts card) =>
        (card.SubsystemDeviceId << 16) | (card.SubsystemVendorId & 0xFFFF);
}
