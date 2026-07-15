using System;
using System.Globalization;
using System.Management;
using System.Runtime.Versioning;
using System.Threading.Tasks;

namespace DashDetective.Tabs.Hardware;

/// <summary>
/// Reads the machine's static hardware facts for the Hardware tab from WMI, following the same
/// async-provider idiom as the Dashboard's <c>SystemInfoProvider</c>: <see cref="GetAsync"/> runs the
/// blocking WMI queries on a background thread, an <see cref="OperatingSystem.IsWindows"/> guard
/// doubles as the platform check, and each per-card section fails independently to its
/// <c>.Unknown</c> record so one dead source can't blank the others — the read never throws.
///
/// The queries are kept Hardware-local (not shared with the Dashboard's providers) because this tab
/// needs richer fields than the Dashboard exposes; per the repo convention a helper only moves to
/// <c>src/Services</c> once a second tab needs the same reading.
///
/// Sections are filled in one phase per card; a section still returns its <c>.Unknown</c> until its
/// phase lands (every field then renders "—").
/// </summary>
public static class HardwareInfoProvider {
    public static Task<HardwareInfo> GetAsync() => Task.Run(Read);

    private static HardwareInfo Read() {
        // Guard doubles as the platform-compatibility check for the WMI calls in each section.
        if (!OperatingSystem.IsWindows())
            return HardwareInfo.Unknown;

        return new HardwareInfo(
            ReadProcessor(), ReadMemory(), ReadStorage(), ReadMotherboard(), ReadGraphics());
    }

    /// <summary>
    /// Processor facts from <c>Win32_Processor</c>. Core/thread counts are summed across sockets and
    /// the clock is the max, matching <c>CpuInfoProvider</c>; name/cache/socket come from the first
    /// package. Boost clock and TDP have no WMI source, so they stay "—".
    /// </summary>
    [SupportedOSPlatform("windows")]
    private static ProcessorInfo ReadProcessor() {
        try {
            var name = "";
            var socket = "";
            int cores = 0, threads = 0;
            double maxClockMhz = 0;
            long l3CacheKb = 0;
            var found = false;

            using var searcher = new ManagementObjectSearcher(
                "SELECT Name, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed, L3CacheSize, " +
                "SocketDesignation FROM Win32_Processor");
            using var results = searcher.Get();

            foreach (var obj in results) {
                using (obj) {
                    found = true;
                    cores += ToInt(obj["NumberOfCores"]);
                    threads += ToInt(obj["NumberOfLogicalProcessors"]);
                    maxClockMhz = Math.Max(maxClockMhz, ToInt(obj["MaxClockSpeed"]));
                    // Name / cache / socket describe a single package; take the first non-empty.
                    if (string.IsNullOrEmpty(name) && obj["Name"] is string n && !string.IsNullOrWhiteSpace(n))
                        name = n.Trim();
                    if (l3CacheKb == 0)
                        l3CacheKb = ToInt(obj["L3CacheSize"]);
                    if (string.IsNullOrEmpty(socket) && obj["SocketDesignation"] is string s && !string.IsNullOrWhiteSpace(s))
                        socket = s.Trim();
                }
            }

            if (!found)
                return ProcessorInfo.Unknown;
            if (threads == 0)
                threads = Environment.ProcessorCount;

            return new ProcessorInfo(
                Name: string.IsNullOrEmpty(name) ? "—" : name,
                CoresThreads: cores > 0 ? $"{cores} / {threads}" : $"— / {threads}",
                // WMI's MaxClockSpeed is the rated/base speed; there is no turbo/boost value.
                BaseBoost: maxClockMhz > 0 ? $"{FormatGhz(maxClockMhz)} / —" : "—",
                CacheL3: l3CacheKb > 0 ? $"{l3CacheKb / 1024} MB" : "—",
                Tdp: "—",
                Socket: string.IsNullOrEmpty(socket) ? "—" : socket);
        } catch {
            return ProcessorInfo.Unknown;
        }
    }

    /// <summary>Memory facts from <c>Win32_PhysicalMemory</c> + <c>Win32_PhysicalMemoryArray</c>. (Phase 3)</summary>
    [SupportedOSPlatform("windows")]
    private static MemoryInfo ReadMemory() {
        try {
            return MemoryInfo.Unknown;
        } catch {
            return MemoryInfo.Unknown;
        }
    }

    /// <summary>Drive facts from <c>Win32_DiskDrive</c> + <c>MSFT_PhysicalDisk</c>. (Phase 4)</summary>
    [SupportedOSPlatform("windows")]
    private static StorageInfo ReadStorage() {
        try {
            return StorageInfo.Unknown;
        } catch {
            return StorageInfo.Unknown;
        }
    }

    /// <summary>Board facts from <c>Win32_BaseBoard</c> + <c>Win32_BIOS</c> + <c>Win32_SystemSlot</c>. (Phase 5)</summary>
    [SupportedOSPlatform("windows")]
    private static MotherboardInfo ReadMotherboard() {
        try {
            return MotherboardInfo.Unknown;
        } catch {
            return MotherboardInfo.Unknown;
        }
    }

    /// <summary>Graphics facts from <c>Win32_VideoController</c>. (Phase 6)</summary>
    [SupportedOSPlatform("windows")]
    private static GraphicsInfo ReadGraphics() {
        try {
            return GraphicsInfo.Unknown;
        } catch {
            return GraphicsInfo.Unknown;
        }
    }

    /// <summary>Formats a clock speed in MHz as GHz to one decimal (e.g. 3200 → "3.2 GHz").</summary>
    private static string FormatGhz(double mhz) =>
        (mhz / 1000.0).ToString("0.0", CultureInfo.InvariantCulture) + " GHz";

    private static int ToInt(object? value) => value is null ? 0 : Convert.ToInt32(value);
}
