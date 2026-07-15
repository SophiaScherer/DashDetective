using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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

    /// <summary>
    /// Memory facts from <c>Win32_PhysicalMemory</c> (per-module capacity/speed/type/voltage) plus
    /// <c>Win32_PhysicalMemoryArray.MemoryDevices</c> for the total slot count. Timings have no WMI
    /// source (SPD/SMBus only), so that row stays "—".
    /// </summary>
    [SupportedOSPlatform("windows")]
    private static MemoryInfo ReadMemory() {
        try {
            var moduleGbs = new List<double>();
            ulong totalBytes = 0;
            int speed = 0, memoryType = 0, voltageMv = 0;

            using (var searcher = new ManagementObjectSearcher(
                "SELECT Capacity, Speed, ConfiguredClockSpeed, SMBIOSMemoryType, ConfiguredVoltage " +
                "FROM Win32_PhysicalMemory"))
            using (var results = searcher.Get()) {
                foreach (var obj in results) {
                    using (obj) {
                        var bytes = ToUInt64(obj["Capacity"]);
                        totalBytes += bytes;
                        moduleGbs.Add(bytes / (double)(1L << 30));
                        // ConfiguredClockSpeed is the actual running speed (what Task Manager shows);
                        // fall back to the rated Speed. Take the highest across modules.
                        speed = Math.Max(speed, Math.Max(ToInt(obj["ConfiguredClockSpeed"]), ToInt(obj["Speed"])));
                        if (memoryType == 0)
                            memoryType = ToInt(obj["SMBIOSMemoryType"]);
                        if (voltageMv == 0)
                            voltageMv = ToInt(obj["ConfiguredVoltage"]);
                    }
                }
            }

            if (moduleGbs.Count == 0)
                return MemoryInfo.Unknown;

            var totalGb = totalBytes / (double)(1L << 30);
            var type = MemoryTypeLabel(memoryType);
            var populated = moduleGbs.Count;
            var totalSlots = ReadMemorySlotCount();

            return new MemoryInfo(
                Summary: speed > 0
                    ? $"{FormatGb(totalGb)} GB {type}-{speed}"
                    : $"{FormatGb(totalGb)} GB {type}",
                Installed: FormatModules(moduleGbs),
                Speed: speed > 0 ? $"{speed} MT/s" : "—",
                Timings: "—",
                SlotsUsed: totalSlots > 0 ? $"{populated} / {totalSlots}" : populated.ToString(),
                Voltage: voltageMv > 0
                    ? (voltageMv / 1000.0).ToString("0.##", CultureInfo.InvariantCulture) + " V"
                    : "—");
        } catch {
            return MemoryInfo.Unknown;
        }
    }

    /// <summary>Total DIMM slots on the board from <c>Win32_PhysicalMemoryArray</c> (0 if unavailable).</summary>
    [SupportedOSPlatform("windows")]
    private static int ReadMemorySlotCount() {
        try {
            using var searcher = new ManagementObjectSearcher(
                "SELECT MemoryDevices FROM Win32_PhysicalMemoryArray");
            using var results = searcher.Get();
            foreach (var obj in results) {
                using (obj) {
                    var slots = ToInt(obj["MemoryDevices"]);
                    if (slots > 0)
                        return slots;
                }
            }
        } catch {
            // Fall through to 0 — the slot count is best-effort.
        }

        return 0;
    }

    /// <summary>
    /// Drive facts, one row per physical disk. Primary source is <c>MSFT_PhysicalDisk</c>
    /// (<c>root\Microsoft\Windows\Storage</c>), which gives the friendly model, size, media/bus type
    /// (SSD/HDD/NVMe) and health in one place. If that namespace is unavailable it falls back to
    /// <c>Win32_DiskDrive</c> for model + size only (type/health then read "—").
    /// </summary>
    [SupportedOSPlatform("windows")]
    private static StorageInfo ReadStorage() {
        try {
            var devices = new List<StorageDeviceInfo>();
            var healthCodes = new List<int>();
            ulong totalBytes = 0;
            var haveHealth = false;

            // Primary: the Storage-management namespace (model + size + type + health).
            try {
                var scope = new ManagementScope(@"\\.\root\Microsoft\Windows\Storage");
                var query = new ObjectQuery(
                    "SELECT FriendlyName, Size, MediaType, BusType, HealthStatus FROM MSFT_PhysicalDisk");
                using var searcher = new ManagementObjectSearcher(scope, query);
                using var results = searcher.Get();
                foreach (var obj in results) {
                    using (obj) {
                        var model = (obj["FriendlyName"] as string)?.Trim();
                        var bytes = ToUInt64(obj["Size"]);
                        var type = DriveTypeLabel(ToInt(obj["MediaType"]), ToInt(obj["BusType"]));
                        totalBytes += bytes;
                        devices.Add(new StorageDeviceInfo(
                            string.IsNullOrWhiteSpace(model) ? "Drive" : model,
                            FormatDriveDetail(bytes, type)));
                        healthCodes.Add(ToInt(obj["HealthStatus"]));
                        haveHealth = true;
                    }
                }
            } catch {
                // Storage namespace unavailable — reset and fall back below.
                devices.Clear();
                healthCodes.Clear();
                totalBytes = 0;
                haveHealth = false;
            }

            // Fallback: classic Win32_DiskDrive (no media type or health).
            if (devices.Count == 0) {
                using var searcher = new ManagementObjectSearcher("SELECT Model, Size FROM Win32_DiskDrive");
                using var results = searcher.Get();
                foreach (var obj in results) {
                    using (obj) {
                        var model = (obj["Model"] as string)?.Trim();
                        var bytes = ToUInt64(obj["Size"]);
                        totalBytes += bytes;
                        devices.Add(new StorageDeviceInfo(
                            string.IsNullOrWhiteSpace(model) ? "Drive" : model,
                            FormatDriveDetail(bytes, "")));
                    }
                }
            }

            if (devices.Count == 0)
                return StorageInfo.Unknown;

            var noun = devices.Count == 1 ? "drive" : "drives";
            return new StorageInfo(
                Summary: $"{devices.Count} {noun} · {FormatDriveSize(totalBytes)} total",
                Drives: devices,
                TotalHealth: haveHealth ? AggregateHealth(healthCodes) : "—");
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

    /// <summary>Formats a GB figure without a trailing ".0" for whole values (16.0 → "16", 1.5 → "1.5").</summary>
    private static string FormatGb(double gb) =>
        gb.ToString(gb % 1 == 0 ? "0" : "0.#", CultureInfo.InvariantCulture);

    /// <summary>Renders the module layout: "2 × 16 GB" when uniform, else "16 GB + 8 GB".</summary>
    private static string FormatModules(IReadOnlyList<double> moduleGbs) {
        if (moduleGbs.Count == 0)
            return "—";
        if (moduleGbs.Distinct().Count() == 1)
            return $"{moduleGbs.Count} × {FormatGb(moduleGbs[0])} GB";
        return string.Join(" + ", moduleGbs.Select(g => $"{FormatGb(g)} GB"));
    }

    /// <summary>Media/bus type label for a physical disk: NVMe wins over the SSD/HDD media flag; "" if
    /// neither is known (the row then shows size only).</summary>
    private static string DriveTypeLabel(int mediaType, int busType) {
        if (busType == 17) return "NVMe";   // BusType 17 = NVMe
        if (mediaType == 4) return "SSD";    // MediaType 4 = SSD
        if (mediaType == 3) return "HDD";    // MediaType 3 = HDD
        return "";
    }

    /// <summary>A drive row's value: capacity plus optional type, e.g. "2 TB NVMe" or "500 GB".</summary>
    private static string FormatDriveDetail(ulong bytes, string type) {
        if (bytes == 0)
            return string.IsNullOrEmpty(type) ? "—" : type;
        var size = FormatDriveSize(bytes);
        return string.IsNullOrEmpty(type) ? size : $"{size} {type}";
    }

    /// <summary>Formats drive capacity in decimal (marketing) units — TB at/above 1 TB, else GB —
    /// dropping a trailing ".0" (2000398934016 → "2 TB").</summary>
    private static string FormatDriveSize(ulong bytes) {
        const double tb = 1_000_000_000_000d, gb = 1_000_000_000d;
        return bytes >= tb
            ? (bytes / tb).ToString("0.#", CultureInfo.InvariantCulture) + " TB"
            : (bytes / gb).ToString("0.#", CultureInfo.InvariantCulture) + " GB";
    }

    /// <summary>Worst-status-wins summary of the drives' HealthStatus codes (0 Healthy, 1 Warning,
    /// 2 Unhealthy; anything else Unknown).</summary>
    private static string AggregateHealth(IReadOnlyList<int> codes) {
        if (codes.Count == 0) return "—";
        if (codes.Contains(2)) return "Unhealthy";
        if (codes.Contains(1)) return "Warning";
        if (codes.All(c => c == 0)) return "Good";
        return "—";
    }

    /// <summary>Maps an SMBIOS memory-type code to a human label, falling back to "RAM".</summary>
    private static string MemoryTypeLabel(int smbiosType) => smbiosType switch {
        20 => "DDR",
        21 => "DDR2",
        24 => "DDR3",
        26 => "DDR4",
        34 => "DDR5",
        27 => "LPDDR",
        28 => "LPDDR2",
        29 => "LPDDR3",
        30 => "LPDDR4",
        35 => "LPDDR5",
        _ => "RAM",
    };

    private static int ToInt(object? value) => value is null ? 0 : Convert.ToInt32(value);

    private static ulong ToUInt64(object? value) => value is null ? 0 : Convert.ToUInt64(value);
}
