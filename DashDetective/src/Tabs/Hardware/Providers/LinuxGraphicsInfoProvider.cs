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
/// The spec rows need a named adapter, so this reads <see cref="DrmCardFacts.ReadNamed"/>; without a pci.ids name the catalog lookup misses and they stay "—".
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

    /// <summary>False when pci.ids lists several models under one device id, since the catalog would fill rows from whichever it matches first.</summary>
    internal static bool NamesOneModel(string productName) => !productName.Contains('/');

    /// <summary>The adapter's VRAM, or <c>null</c> when the driver publishes none so the caller can fall
    /// back.</summary>
    private static string? FormatVram(ulong bytes) =>
        bytes > 0 ? FileSizeFormatter.Format((long)bytes) : null;
}
