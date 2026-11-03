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
/// <b>The spec rows depend on the adapter being named.</b> The kernel identifies an adapter by its PCI ids
/// alone, so this reads through <see cref="DrmCardFacts.ReadNamed"/>, which resolves the marketing model
/// from the system's <c>pci.ids</c> table — that name is what <see cref="HardwareCatalog.LookupGpu"/>
/// matches on, and core counts, boost clocks and bus widths come from the catalogue entry it finds. A host
/// with no <c>pci.ids</c>, a part the catalogue does not list, or a table name covering several models
/// leaves those rows "—". VRAM is
/// independent of all that: amdgpu publishes it exactly, and it is formatted with the same humanizer as
/// the Performance tab's VRAM tile.
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

            foreach (var card in DrmCardFacts.ReadNamed(_proc)) {
                if (card.IsSoftware)
                    continue;

                var spec = NamesOneModel(card.ProductName) ? HardwareCatalog.LookupGpu(card.AdapterName) : null;
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

    /// <summary>
    /// False when <c>pci.ids</c> lists several models under one device id ("Navi 32 [Radeon RX 7700 XT /
    /// 7800 XT]"). The catalog would match whichever model it lists first and fill a 7800 XT's rows with a
    /// 7700 XT's datasheet, so such a name gets no lookup at all. Pure; unit-tested.
    /// </summary>
    internal static bool NamesOneModel(string productName) => !productName.Contains('/');

    /// <summary>The adapter's VRAM, or <c>null</c> when the driver publishes none so the caller can fall
    /// back.</summary>
    private static string? FormatVram(ulong bytes) =>
        bytes > 0 ? FileSizeFormatter.Format((long)bytes) : null;
}
