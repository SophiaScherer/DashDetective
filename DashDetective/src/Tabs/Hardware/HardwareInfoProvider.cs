using System;
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

    /// <summary>Processor facts from <c>Win32_Processor</c>. (Phase 2)</summary>
    [SupportedOSPlatform("windows")]
    private static ProcessorInfo ReadProcessor() {
        try {
            return ProcessorInfo.Unknown;
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
}
