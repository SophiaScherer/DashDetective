using DashDetective.Services.Diagnostics;
using DashDetective.Shared;
using System;
using System.Management;
using System.Runtime.Versioning;
using System.Threading.Tasks;

namespace DashDetective.Tabs.Dashboard;

/// <summary>
/// Reads static CPU hardware information from WMI (<c>Win32_Processor</c>). The query is
/// comparatively slow (~100–300 ms) and blocking, so it runs on a background thread and is
/// awaited once at startup. Any failure yields <see cref="CpuStaticInfo.Unknown"/> rather than
/// throwing. The platform check lives in <c>HardwareProviders.ForCurrentPlatform</c>.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsCpuInfoProvider : ICpuInfoProvider {
    public Task<CpuStaticInfo> GetAsync() => Task.Run(Read);

    private static CpuStaticInfo Read() {
        try {
            var name = Placeholders.UnknownProcessor;
            int physical = 0, logical = 0;
            double maxClock = 0;
            var found = false;

            using var searcher = new ManagementObjectSearcher(
                "SELECT Name, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed FROM Win32_Processor");
            using var results = searcher.Get();

            // Sum core counts across sockets so multi-CPU machines report correctly.
            foreach (var obj in results) {
                using (obj) {
                    found = true;
                    if (obj["Name"] is string n && !string.IsNullOrWhiteSpace(n))
                        name = n.Trim();
                    physical += ToInt(obj["NumberOfCores"]);
                    logical += ToInt(obj["NumberOfLogicalProcessors"]);
                    maxClock = Math.Max(maxClock, ToInt(obj["MaxClockSpeed"]));
                }
            }

            if (!found)
                return CpuStaticInfo.Unknown;
            if (logical == 0)
                logical = Environment.ProcessorCount;

            return new CpuStaticInfo(name, physical, logical, maxClock);
        } catch (Exception e) {
            Log.Warn("CpuInfoProvider read failed", e);
            return CpuStaticInfo.Unknown;
        }
    }

    private static int ToInt(object? value) => value is null ? 0 : Convert.ToInt32(value);
}

/// <summary>The no-CPU-facts set — what the old <c>OperatingSystem.IsWindows()</c> guard returned.</summary>
internal sealed class UnsupportedCpuInfoProvider : ICpuInfoProvider {
    public Task<CpuStaticInfo> GetAsync() => Task.FromResult(CpuStaticInfo.Unknown);
}
