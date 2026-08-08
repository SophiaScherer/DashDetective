using DashDetective.Services.Diagnostics;
using DashDetective.Tabs.Hardware.Catalog;
using System;
using System.Collections.Generic;
using System.Runtime.Versioning;
using System.Threading.Tasks;

namespace DashDetective.Tabs.Hardware;

/// <summary>
/// Graphics facts from <c>Win32_VideoController</c> — <b>every</b> physical adapter's name and Windows
/// driver version, since a machine can have a discrete GPU alongside an integrated one and the Dashboard
/// and Performance tabs already show both. Filtered to PCI-bus adapters, skipping virtual/software ones.
/// VRAM (<c>AdapterRAM</c> is 4 GB-capped and misleading), memory type, CUDA-core count, boost clock and
/// bus width have no reliable WMI source, so they come from <see cref="HardwareCatalog"/> by model. The
/// platform check lives in <see cref="IHardwareInfoProvider.ForCurrentPlatform"/>.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsGraphicsInfoProvider : IGraphicsInfoProvider {
    public Task<GraphicsInfo> GetAsync() => Task.Run(Read);

    private static GraphicsInfo Read() {
        try {
            var adapters = new List<GraphicsAdapterInfo>();

            WmiRead.ForEach("SELECT Name, PNPDeviceID, DriverVersion FROM Win32_VideoController", obj => {
                // Physical GPUs sit on the PCI bus; virtual/software adapters are ROOT\/SWD\.
                var pnp = obj["PNPDeviceID"] as string;
                if (pnp is null || !pnp.StartsWith(@"PCI\", StringComparison.OrdinalIgnoreCase))
                    return;

                var name = WmiRead.Text(obj, "Name");
                if (string.IsNullOrEmpty(name))
                    return;

                var driver = WmiRead.Text(obj, "DriverVersion");
                var spec = HardwareCatalog.LookupGpu(name);
                adapters.Add(new GraphicsAdapterInfo(
                    Name: name,
                    Memory: spec?.Memory ?? "—",
                    CudaCores: spec?.CudaCores ?? "—",
                    BoostClock: spec?.BoostClock ?? "—",
                    Driver: string.IsNullOrEmpty(driver) ? "—" : driver,
                    Bus: spec?.Bus ?? "—"));
            });

            return adapters.Count == 0 ? GraphicsInfo.Unknown : new GraphicsInfo(adapters);
        } catch (Exception e) {
            Log.Warn("GraphicsInfoProvider read failed", e);
            return GraphicsInfo.Unknown;
        }
    }
}

/// <summary>The no-adapters contract, until the Linux GPU milestone lands its reader.</summary>
internal sealed class UnsupportedGraphicsInfoProvider : IGraphicsInfoProvider {
    public Task<GraphicsInfo> GetAsync() => Task.FromResult(GraphicsInfo.Unknown);
}
