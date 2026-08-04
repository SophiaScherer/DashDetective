using DashDetective.Shared;
using DashDetective.Tabs.Hardware.Catalog;
using System;
using System.Collections.Generic;
using System.Management;
using System.Runtime.Versioning;
using System.Threading.Tasks;

namespace DashDetective.Tabs.Hardware;

/// <summary>
/// Reads the machine's static hardware facts for the Hardware tab from WMI: <see cref="GetAsync"/> runs
/// the blocking queries on a background thread, and each per-card section fails independently to its
/// <c>.Unknown</c> record so one dead source can't blank the others — the read never throws. The
/// platform check lives in <see cref="IHardwareInfoProvider.ForCurrentPlatform"/>, which is why the
/// class carries one <see cref="SupportedOSPlatformAttribute"/> instead of a guard and nine per-method
/// attributes.
///
/// The queries are kept Hardware-local (not shared with the Dashboard's providers) because this tab
/// needs richer fields than the Dashboard exposes; per the repo convention a helper only moves to
/// <c>src/Services</c> once a second tab needs the same reading.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsHardwareInfoProvider : IHardwareInfoProvider {
    public Task<HardwareInfo> GetAsync() => Task.Run(Read);

    private static HardwareInfo Read() =>
        new(ReadProcessor(), ReadMemory(), ReadStorage(), ReadMotherboard(), ReadGraphics());

    /// <summary>
    /// Processor facts from <c>Win32_Processor</c>. Core/thread counts are summed across sockets and
    /// the clock is the max, matching <c>CpuInfoProvider</c>; name/cache/socket come from the first
    /// package. Boost clock and TDP have no WMI source, so they stay "—".
    /// </summary>
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
                Cores: cores > 0 ? cores.ToString() : "—",
                LogicalProcessors: threads.ToString(),
                BaseBoost: ProcessorSpecFormatter.BaseBoost(maxClockMhz, spec?.Boost),
                CacheL3: ProcessorSpecFormatter.CacheL3(l3CacheKb),
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
    private static MemoryInfo ReadMemory() {
        try {
            var moduleGbs = new List<double>();
            ulong totalBytes = 0;
            int speed = 0, memoryType = 0, voltageMv = 0;
            var partNumber = "";

            using (var searcher = new ManagementObjectSearcher(
                "SELECT Capacity, Speed, ConfiguredClockSpeed, SMBIOSMemoryType, ConfiguredVoltage, " +
                "PartNumber FROM Win32_PhysicalMemory"))
            using (var results = searcher.Get()) {
                foreach (var obj in results) {
                    using (obj) {
                        var bytes = ToUInt64(obj["Capacity"]);
                        totalBytes += bytes;
                        moduleGbs.Add(bytes / (double)(1L << 30));
                        // The shared MemorySpeed rule prefers the running speed over the rated one, so this
                        // reads the same as the Dashboard's RAM line. Take the highest across modules.
                        speed = Math.Max(speed,
                            MemorySpeed.Running(ToInt(obj["ConfiguredClockSpeed"]), ToInt(obj["Speed"])));
                        if (memoryType == 0)
                            memoryType = ToInt(obj["SMBIOSMemoryType"]);
                        if (voltageMv == 0)
                            voltageMv = ToInt(obj["ConfiguredVoltage"]);
                        if (string.IsNullOrEmpty(partNumber) && obj["PartNumber"] is string pn && !string.IsNullOrWhiteSpace(pn))
                            partNumber = pn.Trim();
                    }
                }
            }

            if (moduleGbs.Count == 0)
                return MemoryInfo.Unknown;

            var totalGb = totalBytes / (double)(1L << 30);
            var type = MemorySpecFormatter.TypeLabel(memoryType);

            // Timings aren't in WMI — fill from the spec catalog by module part number (rated profile).
            var timings = HardwareCatalog.LookupMemory(partNumber)?.Timings ?? "—";

            return new MemoryInfo(
                Summary: MemorySpecFormatter.Summary(totalGb, type, speed),
                Installed: MemorySpecFormatter.Modules(moduleGbs),
                Speed: MemorySpecFormatter.Speed(speed),
                Timings: timings,
                SlotsUsed: MemorySpecFormatter.SlotsUsed(moduleGbs.Count, ReadMemorySlotCount()),
                Voltage: MemorySpecFormatter.Voltage(voltageMv));
        } catch {
            return MemoryInfo.Unknown;
        }
    }

    /// <summary>Total DIMM slots on the board from <c>Win32_PhysicalMemoryArray</c> (0 if unavailable).</summary>
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
                        var type = StorageSpecFormatter.TypeLabel(ToInt(obj["MediaType"]), ToInt(obj["BusType"]));
                        totalBytes += bytes;
                        devices.Add(new StorageDeviceInfo(
                            string.IsNullOrWhiteSpace(model) ? "Drive" : model,
                            StorageSpecFormatter.DriveDetail(bytes, type)));
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
                            StorageSpecFormatter.DriveDetail(bytes, "")));
                    }
                }
            }

            if (devices.Count == 0)
                return StorageInfo.Unknown;

            return new StorageInfo(
                Summary: StorageSpecFormatter.Summary(devices.Count, totalBytes),
                Drives: devices,
                TotalHealth: haveHealth ? StorageSpecFormatter.Health(healthCodes) : "—");
        } catch {
            return StorageInfo.Unknown;
        }
    }

    /// <summary>
    /// Board facts: vendor + product from <c>Win32_BaseBoard</c>, BIOS version + release year from
    /// <c>Win32_BIOS</c>, and a best-effort PCIe slot count from <c>Win32_SystemSlot</c> (slots whose
    /// designation names PCI/PCIe). Chipset, form factor and M.2 count have no WMI source → "—".
    /// </summary>
    private static MotherboardInfo ReadMotherboard() {
        try {
            var manufacturer = FirstString("SELECT Manufacturer, Product FROM Win32_BaseBoard", "Manufacturer");
            var product = FirstString("SELECT Manufacturer, Product FROM Win32_BaseBoard", "Product");
            var board = Join(manufacturer, product);
            var bios = ReadBios();
            var pcie = ReadPcieSlotCount();

            // Chipset/form-factor/M.2 aren't in WMI. Form factor + M.2 come from the board catalog;
            // chipset prefers the catalog but falls back to a name-token derivation so most boards
            // resolve it without per-board data.
            var spec = HardwareCatalog.LookupBoard(product);
            var chipset = spec?.Chipset ?? DeriveChipset(product);

            return new MotherboardInfo(
                Board: string.IsNullOrEmpty(board) ? "—" : board,
                Chipset: string.IsNullOrEmpty(chipset) ? "—" : chipset,
                Bios: string.IsNullOrEmpty(bios) ? "—" : bios,
                FormFactor: spec?.FormFactor ?? "—",
                PcieSlots: pcie > 0 ? pcie.ToString() : "—",
                M2Slots: spec?.M2Slots ?? "—");
        } catch {
            return MotherboardInfo.Unknown;
        }
    }

    /// <summary>Chipset (vendor + model) tokens looked up in the board product string, more-specific
    /// variants first (e.g. B650E before B650) so the derived label is the exact chipset.</summary>
    private static readonly (string Token, string Label)[] Chipsets = {
        // AMD (AM5 / AM4)
        ("X670E", "AMD X670E"), ("X670", "AMD X670"), ("B650E", "AMD B650E"), ("B650", "AMD B650"),
        ("A620", "AMD A620"), ("X570", "AMD X570"), ("B550", "AMD B550"), ("A520", "AMD A520"),
        ("X470", "AMD X470"), ("B450", "AMD B450"),
        // Intel (LGA 1700)
        ("Z790", "Intel Z790"), ("Z690", "Intel Z690"), ("B760", "Intel B760"), ("B660", "Intel B660"),
        ("H770", "Intel H770"), ("H670", "Intel H670"), ("Q670", "Intel Q670"), ("H610", "Intel H610"),
    };

    /// <summary>Best-effort chipset from the board product name (e.g. "MPG B650I EDGE" → "AMD B650");
    /// "" when no known token is present.</summary>
    private static string DeriveChipset(string product) {
        if (string.IsNullOrWhiteSpace(product))
            return "";
        var upper = product.ToUpperInvariant();
        foreach (var (token, label) in Chipsets) {
            if (upper.Contains(token, StringComparison.Ordinal))
                return label;
        }

        return "";
    }

    /// <summary>BIOS version plus release year, e.g. "1203 (2024)".</summary>
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
    /// Graphics facts from <c>Win32_VideoController</c> — <b>every</b> physical adapter's name and Windows
    /// driver version, since a machine can have a discrete GPU alongside an integrated one and the Dashboard
    /// and Performance tabs already show both. Filtered to PCI-bus adapters, skipping virtual/software ones.
    /// VRAM (<c>AdapterRAM</c> is 4 GB-capped and misleading),
    /// memory type, CUDA-core count, boost clock and bus width have no reliable WMI source → "—".
    /// </summary>
    private static GraphicsInfo ReadGraphics() {
        try {
            var adapters = new List<GraphicsAdapterInfo>();

            using (var searcher = new ManagementObjectSearcher(
                "SELECT Name, PNPDeviceID, DriverVersion FROM Win32_VideoController"))
            using (var results = searcher.Get()) {
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
                        // Memory/CUDA/boost/bus aren't in WMI — fill them from the spec catalog by model.
                        var spec = HardwareCatalog.LookupGpu(name);
                        adapters.Add(new GraphicsAdapterInfo(
                            Name: name.Trim(),
                            Memory: spec?.Memory ?? "—",
                            CudaCores: spec?.CudaCores ?? "—",
                            BoostClock: spec?.BoostClock ?? "—",
                            Driver: string.IsNullOrWhiteSpace(driver) ? "—" : driver.Trim(),
                            Bus: spec?.Bus ?? "—"));
                    }
                }
            }

            return adapters.Count == 0 ? GraphicsInfo.Unknown : new GraphicsInfo(adapters);
        } catch {
            return GraphicsInfo.Unknown;
        }
    }

    /// <summary>Returns the first non-empty string value of <paramref name="property"/> from a WMI query
    /// (the <c>SystemInfoProvider.QueryString</c> idiom).</summary>
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

/// <summary>The no-inventory set: every card reports <c>.Unknown</c> and so renders "—", which is what
/// the old <c>OperatingSystem.IsWindows()</c> guard returned off Windows.</summary>
internal sealed class UnsupportedHardwareInfoProvider : IHardwareInfoProvider {
    public Task<HardwareInfo> GetAsync() => Task.FromResult(HardwareInfo.Unknown);
}
