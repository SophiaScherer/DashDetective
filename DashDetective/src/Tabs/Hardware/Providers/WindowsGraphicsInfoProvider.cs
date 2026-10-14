using DashDetective.Services.Diagnostics;
using DashDetective.Services.Platform.Windows;
using DashDetective.Tabs.Hardware.Catalog;
using System;
using System.Collections.Generic;
using System.Runtime.Versioning;
using System.Threading.Tasks;

namespace DashDetective.Tabs.Hardware;

/// <summary>
/// Graphics facts from <c>Win32_VideoController</c> (through the shared <see cref="VideoControllers"/>) —
/// <b>every</b> physical adapter's name and Windows driver version, since a machine can have a discrete GPU
/// alongside an integrated one and the Dashboard and Performance tabs already show both.
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

            // The same rows the Performance tab's GPU details read, so both show one driver version.
            foreach (var controller in VideoControllers.Read()) {
                if (string.IsNullOrEmpty(controller.Name))
                    continue;

                var spec = HardwareCatalog.LookupGpu(controller.Name);
                adapters.Add(new GraphicsAdapterInfo(
                    Name: controller.Name,
                    Memory: spec?.Memory ?? "—",
                    CudaCores: spec?.CudaCores ?? "—",
                    BoostClock: spec?.BoostClock ?? "—",
                    Driver: string.IsNullOrEmpty(controller.DriverVersion) ? "—" : controller.DriverVersion,
                    Bus: spec?.Bus ?? "—"));
            }

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
