using DashDetective.Services.Diagnostics;
using DashDetective.Tabs.Hardware.Catalog;
using System;
using System.Runtime.Versioning;
using System.Threading.Tasks;

namespace DashDetective.Tabs.Hardware;

/// <summary>
/// Processor facts from <c>Win32_Processor</c>. Core/thread counts are summed across sockets and the
/// clock is the max, matching <c>CpuInfoProvider</c>; name/cache/socket come from the first package.
/// Boost clock and TDP have no WMI source, so they come from <see cref="HardwareCatalog"/> by model name
/// and stay "—" when it has no entry. Base clock, L3 size and socket <i>do</i> have a WMI source and keep
/// it — the catalog fills those three only where the firmware left the field empty. The platform check
/// lives in <see cref="IHardwareInfoProvider.ForCurrentPlatform"/>.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsProcessorInfoProvider : IProcessorInfoProvider {
    public Task<ProcessorInfo> GetAsync() => Task.Run(Read);

    private static ProcessorInfo Read() {
        try {
            var name = "";
            var socket = "";
            int cores = 0, threads = 0;
            double maxClockMhz = 0;
            long l3CacheKb = 0;
            var found = false;

            WmiRead.ForEach(
                "SELECT Name, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed, L3CacheSize, " +
                "SocketDesignation FROM Win32_Processor",
                obj => {
                    found = true;
                    cores += WmiRead.ToInt(obj["NumberOfCores"]);
                    threads += WmiRead.ToInt(obj["NumberOfLogicalProcessors"]);
                    maxClockMhz = Math.Max(maxClockMhz, WmiRead.ToInt(obj["MaxClockSpeed"]));
                    // Name / cache / socket describe a single package; take the first non-empty.
                    if (string.IsNullOrEmpty(name))
                        name = WmiRead.Text(obj, "Name");
                    if (l3CacheKb == 0)
                        l3CacheKb = WmiRead.ToInt(obj["L3CacheSize"]);
                    if (string.IsNullOrEmpty(socket))
                        socket = WmiRead.Text(obj, "SocketDesignation");
                });

            if (!found)
                return ProcessorInfo.Unknown;
            if (threads == 0)
                threads = Environment.ProcessorCount;

            var spec = HardwareCatalog.LookupCpu(name);

            // WMI's designation wins where the firmware filled it in: it names the board's own socket,
            // which the catalog's rated socket for the part can only approximate.
            var socketName = string.IsNullOrEmpty(socket)
                ? ProcessorSpecFormatter.Spec(spec?.Socket)
                : socket;

            return new ProcessorInfo(
                Name: string.IsNullOrEmpty(name) ? "—" : name,
                Cores: cores > 0 ? cores.ToString() : "—",
                LogicalProcessors: threads.ToString(),
                BaseBoost: ProcessorSpecFormatter.BaseBoost(maxClockMhz, spec?.Boost, spec?.Base),
                CacheL3: ProcessorSpecFormatter.CacheL3(l3CacheKb, spec?.CacheL3),
                Tdp: ProcessorSpecFormatter.Spec(spec?.Tdp),
                Socket: socketName);
        } catch (Exception e) {
            Log.Warn("ProcessorInfoProvider read failed", e);
            return ProcessorInfo.Unknown;
        }
    }
}
