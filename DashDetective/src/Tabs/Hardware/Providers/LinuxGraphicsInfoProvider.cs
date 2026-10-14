using DashDetective.Services.Diagnostics;
using DashDetective.Services.Platform.Linux;
using DashDetective.Tabs.FileExplorer;
using DashDetective.Tabs.Hardware.Catalog;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DashDetective.Tabs.Hardware;

/// <summary>
/// Graphics facts from <c>/sys/class/drm</c> over the shared <see cref="DrmCardFacts"/> derivation, so this
/// card and the Dashboard's cannot disagree about the same adapter. The Linux counterpart to
/// <see cref="WindowsGraphicsInfoProvider"/>'s <c>Win32_VideoController</c> query.
///
/// <b>Most spec rows stay "—", and that is the honest result.</b> The kernel names an adapter by its PCI
/// ids, not its marketing model, so <see cref="HardwareCatalog.LookupGpu"/> has nothing to match on and
/// core counts, boost clocks and bus widths have no source — the lookup is still attempted, so a richer
/// name source would light those rows up for free. VRAM is the exception: amdgpu publishes it exactly, and
/// it is formatted with the shared byte humanizer.
///
/// Stateless and never throws: any failure yields <see cref="GraphicsInfo.Unknown"/>.
/// </summary>
internal sealed class LinuxGraphicsInfoProvider : IGraphicsInfoProvider {
    private readonly IProcFileSystem _proc;

    public LinuxGraphicsInfoProvider() : this(new ProcFileSystem()) { }

    /// <summary>Test seam: injects the filesystem so the walk can be exercised against canned fixtures from
    /// any dev machine.</summary>
    internal LinuxGraphicsInfoProvider(IProcFileSystem proc) => _proc = proc;

    public Task<GraphicsInfo> GetAsync() => Task.Run(Read);

    private GraphicsInfo Read() {
        try {
            var adapters = new List<GraphicsAdapterInfo>();

            foreach (var card in DrmCardFacts.Read(_proc)) {
                if (card.IsSoftware)
                    continue;

                var spec = HardwareCatalog.LookupGpu(card.AdapterName);
                adapters.Add(new GraphicsAdapterInfo(
                    Name: card.AdapterName,
                    Memory: FormatVram(card.VramBytes) ?? spec?.Memory ?? "—",
                    CudaCores: spec?.CudaCores ?? "—",
                    BoostClock: spec?.BoostClock ?? "—",
                    // An in-tree driver such as amdgpu publishes no version of its own; see DrmCardFacts.
                    Driver: card.DriverVersion.Length > 0 ? card.DriverVersion : "—",
                    Bus: spec?.Bus ?? "—"));
            }

            return adapters.Count == 0 ? GraphicsInfo.Unknown : new GraphicsInfo(adapters);
        } catch (Exception e) {
            Log.Warn("LinuxGraphicsInfoProvider read failed", e);
            return GraphicsInfo.Unknown;
        }
    }

    /// <summary>The adapter's VRAM, or <c>null</c> when the driver publishes none so the caller can fall
    /// back.</summary>
    private static string? FormatVram(ulong bytes) =>
        bytes > 0 ? FileSizeFormatter.Format((long)bytes) : null;
}
