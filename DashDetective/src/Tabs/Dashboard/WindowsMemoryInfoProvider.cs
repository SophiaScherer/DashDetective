using DashDetective.Services.Diagnostics;
using DashDetective.Shared;
using System;
using System.Management;
using System.Runtime.Versioning;
using System.Threading.Tasks;

namespace DashDetective.Tabs.Dashboard;

/// <summary>
/// Reads static physical-memory hardware information from WMI (<c>Win32_PhysicalMemory</c>). The
/// query is comparatively slow and blocking, so it runs on a background thread and is awaited once
/// at startup. Any failure yields <see cref="MemoryStaticInfo.Unknown"/> rather than throwing. The
/// platform check lives in <c>HardwareProviders.ForCurrentPlatform</c>.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsMemoryInfoProvider : IMemoryInfoProvider {
    public Task<MemoryStaticInfo> GetAsync() => Task.Run(Read);

    private static MemoryStaticInfo Read() {
        try {
            ulong totalBytes = 0;
            int speed = 0, modules = 0, memoryType = 0;
            var found = false;

            using var searcher = new ManagementObjectSearcher(
                "SELECT Capacity, Speed, ConfiguredClockSpeed, SMBIOSMemoryType FROM Win32_PhysicalMemory");
            using var results = searcher.Get();

            // Sum capacity across every installed module; take the highest reported speed. Speed comes
            // through the shared MemorySpeed rule so this reads the same as the Hardware tab's Speed row.
            foreach (var obj in results) {
                using (obj) {
                    found = true;
                    modules++;
                    totalBytes += ToUInt64(obj["Capacity"]);
                    speed = Math.Max(speed,
                        MemorySpeed.Running(ToInt(obj["ConfiguredClockSpeed"]), ToInt(obj["Speed"])));
                    if (memoryType == 0)
                        memoryType = ToInt(obj["SMBIOSMemoryType"]);
                }
            }

            if (!found)
                return MemoryStaticInfo.Unknown;

            var totalGb = totalBytes / (double)(1L << 30);
            return new MemoryStaticInfo(totalGb, MemoryTypeLabel(memoryType), speed, modules);
        } catch (Exception e) {
            Log.Warn("MemoryInfoProvider read failed", e);
            return MemoryStaticInfo.Unknown;
        }
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

/// <summary>The no-memory-facts set — what the old <c>OperatingSystem.IsWindows()</c> guard returned.</summary>
internal sealed class UnsupportedMemoryInfoProvider : IMemoryInfoProvider {
    public Task<MemoryStaticInfo> GetAsync() => Task.FromResult(MemoryStaticInfo.Unknown);
}
