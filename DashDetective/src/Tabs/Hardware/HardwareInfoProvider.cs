using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Management;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using DashDetective.Tabs.Hardware.Catalog;

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

            // Boost clock and TDP aren't in WMI — fill them from the spec catalog by model name.
            var spec = HardwareCatalog.LookupCpu(name);

            return new ProcessorInfo(
                Name: string.IsNullOrEmpty(name) ? "—" : name,
                CoresThreads: cores > 0 ? $"{cores} / {threads}" : $"— / {threads}",
                BaseBoost: FormatBaseBoost(maxClockMhz, spec?.Boost),
                CacheL3: l3CacheKb > 0 ? $"{l3CacheKb / 1024} MB" : "—",
                Tdp: spec?.Tdp ?? "—",
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

    /// <summary>
    /// Board facts: vendor + product from <c>Win32_BaseBoard</c>, BIOS version + release year from
    /// <c>Win32_BIOS</c>, and a best-effort PCIe slot count from <c>Win32_SystemSlot</c> (slots whose
    /// designation names PCI/PCIe). Chipset, form factor and M.2 count have no WMI source → "—".
    /// </summary>
    [SupportedOSPlatform("windows")]
    private static MotherboardInfo ReadMotherboard() {
        try {
            var board = ReadBoard();
            var bios = ReadBios();
            var pcie = ReadPcieSlotCount();

            return new MotherboardInfo(
                Board: string.IsNullOrEmpty(board) ? "—" : board,
                Chipset: "—",
                Bios: string.IsNullOrEmpty(bios) ? "—" : bios,
                FormFactor: "—",
                PcieSlots: pcie > 0 ? pcie.ToString() : "—",
                M2Slots: "—");
        } catch {
            return MotherboardInfo.Unknown;
        }
    }

    /// <summary>Motherboard vendor + product, e.g. "ASUSTeK COMPUTER INC. ROG STRIX B650E-F".</summary>
    [SupportedOSPlatform("windows")]
    private static string ReadBoard() {
        var manufacturer = FirstString("SELECT Manufacturer, Product FROM Win32_BaseBoard", "Manufacturer");
        var product = FirstString("SELECT Manufacturer, Product FROM Win32_BaseBoard", "Product");
        return Join(manufacturer, product);
    }

    /// <summary>BIOS version plus release year, e.g. "1203 (2024)".</summary>
    [SupportedOSPlatform("windows")]
    private static string ReadBios() {
        var version = FirstString("SELECT SMBIOSBIOSVersion, ReleaseDate FROM Win32_BIOS", "SMBIOSBIOSVersion");
        var releaseDate = FirstString("SELECT SMBIOSBIOSVersion, ReleaseDate FROM Win32_BIOS", "ReleaseDate");
        var year = DmtfYear(releaseDate);
        if (string.IsNullOrEmpty(version))
            return "";
        return year > 0 ? $"{version} ({year})" : version;
    }

    /// <summary>Best-effort count of PCIe slots — <c>Win32_SystemSlot</c> rows whose designation names
    /// PCI/PCIe. Lane width isn't in WMI, so only the count is reported.</summary>
    [SupportedOSPlatform("windows")]
    private static int ReadPcieSlotCount() {
        try {
            var count = 0;
            using var searcher = new ManagementObjectSearcher(
                "SELECT SlotDesignation FROM Win32_SystemSlot");
            using var results = searcher.Get();
            foreach (var obj in results) {
                using (obj) {
                    var designation = obj["SlotDesignation"] as string ?? "";
                    if (designation.IndexOf("PCI", StringComparison.OrdinalIgnoreCase) >= 0)
                        count++;
                }
            }

            return count;
        } catch {
            return 0;
        }
    }

    /// <summary>
    /// Graphics facts from <c>Win32_VideoController</c> — the first physical adapter's name (subtitle)
    /// and Windows driver version. Filtered to PCI-bus adapters (skipping virtual/software ones) the
    /// same way as <c>GpuInfoProvider</c>. VRAM (<c>AdapterRAM</c> is 4 GB-capped and misleading),
    /// memory type, CUDA-core count, boost clock and bus width have no reliable WMI source → "—".
    /// </summary>
    [SupportedOSPlatform("windows")]
    private static GraphicsInfo ReadGraphics() {
        try {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name, PNPDeviceID, DriverVersion FROM Win32_VideoController");
            using var results = searcher.Get();

            foreach (var obj in results) {
                using (obj) {
                    // Physical GPUs sit on the PCI bus; virtual/software adapters are ROOT\/SWD\.
                    var pnp = obj["PNPDeviceID"] as string;
                    if (pnp is null || !pnp.StartsWith(@"PCI\", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var name = obj["Name"] as string;
                    if (string.IsNullOrWhiteSpace(name))
                        continue;

                    var driver = obj["DriverVersion"] as string;
                    return new GraphicsInfo(
                        Name: name.Trim(),
                        Memory: "—",
                        CudaCores: "—",
                        BoostClock: "—",
                        Driver: string.IsNullOrWhiteSpace(driver) ? "—" : driver.Trim(),
                        Bus: "—");
                }
            }

            return GraphicsInfo.Unknown;
        } catch {
            return GraphicsInfo.Unknown;
        }
    }

    /// <summary>Formats a clock speed in MHz as GHz to one decimal (e.g. 3200 → "3.2 GHz").</summary>
    private static string FormatGhz(double mhz) =>
        (mhz / 1000.0).ToString("0.0", CultureInfo.InvariantCulture) + " GHz";

    /// <summary>Composes the "Base / Boost" value from the WMI base clock and the catalog boost string.
    /// When both are known the unit is shared ("4.7 / 5.3 GHz", matching the comp); otherwise the known
    /// side carries its own unit and the missing side is "—".</summary>
    private static string FormatBaseBoost(double baseMhz, string? boost) {
        var hasBase = baseMhz > 0;
        var hasBoost = !string.IsNullOrEmpty(boost);
        if (!hasBase && !hasBoost) return "—";
        if (hasBase && hasBoost)
            return $"{(baseMhz / 1000.0).ToString("0.0", CultureInfo.InvariantCulture)} / {boost}";
        return hasBase ? $"{FormatGhz(baseMhz)} / —" : $"— / {boost}";
    }

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

    /// <summary>Returns the first non-empty string value of <paramref name="property"/> from a WMI query
    /// (the <c>SystemInfoProvider.QueryString</c> idiom).</summary>
    [SupportedOSPlatform("windows")]
    private static string FirstString(string query, string property) {
        using var searcher = new ManagementObjectSearcher(query);
        using var results = searcher.Get();
        foreach (var obj in results) {
            using (obj) {
                if (obj[property] is string s && !string.IsNullOrWhiteSpace(s))
                    return s.Trim();
            }
        }

        return "";
    }

    /// <summary>Joins two parts with a space, skipping blanks (e.g. vendor + product).</summary>
    private static string Join(string first, string second) {
        if (string.IsNullOrWhiteSpace(first)) return second.Trim();
        if (string.IsNullOrWhiteSpace(second)) return first.Trim();
        return $"{first.Trim()} {second.Trim()}";
    }

    /// <summary>Extracts the year from a WMI/DMTF datetime (leading "yyyy…"); 0 if unparseable.</summary>
    private static int DmtfYear(string dmtf) =>
        dmtf.Length >= 4 && int.TryParse(dmtf[..4], out var year) ? year : 0;

    private static int ToInt(object? value) => value is null ? 0 : Convert.ToInt32(value);

    private static ulong ToUInt64(object? value) => value is null ? 0 : Convert.ToUInt64(value);
}
