using DashDetective.Services.Diagnostics;
using System;
using System.Management;
using System.Threading.Tasks;

namespace DashDetective.Tabs.Dashboard;

/// <summary>
/// Reads the static GPU name from WMI (<c>Win32_VideoController</c>). The query is comparatively
/// slow and blocking, so it runs on a background thread and is awaited once at startup. Any failure
/// (or a non-Windows host) yields <see cref="GpuStaticInfo.Unknown"/> rather than throwing.
///
/// Only the adapter <c>Name</c> is read here: <c>Win32_VideoController</c> has no live-utilisation
/// counter (that comes from PDH in <see cref="DashDetective.Services.SystemMetrics.GpuUsageSampler"/>) and its <c>AdapterRAM</c> is
/// capped at 4 GB, so VRAM is deliberately not sourced from it.
/// </summary>
public static class GpuInfoProvider {
    public static Task<GpuStaticInfo> GetAsync() => Task.Run(Read);

    private static GpuStaticInfo Read() {
        // Guard doubles as the platform-compatibility check for the WMI call below.
        if (!OperatingSystem.IsWindows())
            return GpuStaticInfo.Unknown;

        try {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name, PNPDeviceID FROM Win32_VideoController");
            using var results = searcher.Get();

            // Windows enumerates virtual/software adapters (Parsec, RDP mirror, Basic Render)
            // alongside real GPUs, often first. Physical GPUs sit on the PCI bus, so their
            // PNPDeviceID starts with "PCI\" while virtual ones are "ROOT\"/"SWD\" — that bus
            // prefix is the reliable filter. The first physical adapter is surfaced for now;
            // machines with several (this repo's dev box has both an NVIDIA and an AMD GPU) get
            // a card each once multi-GPU support lands.
            foreach (var obj in results) {
                using (obj) {
                    if (!IsPhysicalAdapter(obj["PNPDeviceID"] as string))
                        continue;

                    var name = obj["Name"] as string;
                    if (!string.IsNullOrWhiteSpace(name))
                        return new GpuStaticInfo(name.Trim());
                }
            }

            return GpuStaticInfo.Unknown;
        } catch (Exception e) {
            Log.Warn("GpuInfoProvider read failed", e);
            return GpuStaticInfo.Unknown;
        }
    }

    /// <summary>True when the adapter is enumerated on the PCI bus, i.e. a real graphics device.</summary>
    private static bool IsPhysicalAdapter(string? pnpDeviceId) =>
        pnpDeviceId is not null && pnpDeviceId.StartsWith(@"PCI\", StringComparison.OrdinalIgnoreCase);
}
