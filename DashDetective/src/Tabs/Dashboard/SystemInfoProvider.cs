using Microsoft.Win32;
using System;
using System.Management;
using System.Runtime.Versioning;
using System.Threading.Tasks;

namespace DashDetective.Tabs.Dashboard;

/// <summary>
/// Reads static machine identity from WMI (<c>Win32_OperatingSystem</c>, <c>Win32_BIOS</c>,
/// <c>Win32_BaseBoard</c>), the registry (build revision and feature-update label, which WMI does
/// not expose) and the runtime (<see cref="Environment.MachineName"/>). The WMI queries are
/// comparatively slow and blocking, so the whole read runs on a background thread and is awaited
/// once at startup. Any failure (or a non-Windows host) yields <see cref="SystemStaticInfo.Unknown"/>
/// rather than throwing; each section also falls back independently so one dead source doesn't blank
/// the others.
/// </summary>
public static class SystemInfoProvider {
    private const string CurrentVersionKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";

    public static Task<SystemStaticInfo> GetAsync() => Task.Run(Read);

    private static SystemStaticInfo Read() {
        // Guard doubles as the platform-compatibility check for the WMI/registry calls below.
        if (!OperatingSystem.IsWindows())
            return SystemStaticInfo.Unknown;

        try {
            return new SystemStaticInfo(
                ReadOs(), Environment.MachineName, ReadBios(), ReadBuild(), ReadMotherboard());
        } catch {
            return SystemStaticInfo.Unknown;
        }
    }

    /// <summary>OS edition from WMI plus the registry feature-update label, e.g. "Windows 11 Pro 24H2".</summary>
    [SupportedOSPlatform("windows")]
    private static string ReadOs() {
        try {
            var caption = QueryString("SELECT Caption FROM Win32_OperatingSystem", "Caption");
            if (string.IsNullOrWhiteSpace(caption))
                caption = "Unknown OS";
            // Win32_OperatingSystem reports "Microsoft Windows 11 Pro"; drop the prefix to match
            // the panel's compact style.
            else if (caption.StartsWith("Microsoft ", StringComparison.OrdinalIgnoreCase))
                caption = caption["Microsoft ".Length..];

            var display = ReadRegistryString("DisplayVersion");
            return string.IsNullOrWhiteSpace(display) ? caption : $"{caption} {display}";
        } catch {
            return "Unknown OS";
        }
    }

    /// <summary>BIOS vendor and version from WMI, e.g. "American Megatrends Inc. F31d".</summary>
    [SupportedOSPlatform("windows")]
    private static string ReadBios() {
        try {
            var manufacturer = QueryString("SELECT Manufacturer, SMBIOSBIOSVersion FROM Win32_BIOS", "Manufacturer");
            var version = QueryString("SELECT Manufacturer, SMBIOSBIOSVersion FROM Win32_BIOS", "SMBIOSBIOSVersion");
            var text = Join(manufacturer, version);
            return string.IsNullOrWhiteSpace(text) ? "Unknown BIOS" : text;
        } catch {
            return "Unknown BIOS";
        }
    }

    /// <summary>Motherboard vendor and product from WMI, e.g. "ASUSTeK COMPUTER INC. ROG STRIX Z790-E".</summary>
    [SupportedOSPlatform("windows")]
    private static string ReadMotherboard() {
        try {
            var manufacturer = QueryString("SELECT Manufacturer, Product FROM Win32_BaseBoard", "Manufacturer");
            var product = QueryString("SELECT Manufacturer, Product FROM Win32_BaseBoard", "Product");
            var text = Join(manufacturer, product);
            return string.IsNullOrWhiteSpace(text) ? "Unknown motherboard" : text;
        } catch {
            return "Unknown motherboard";
        }
    }

    /// <summary>Full build number plus revision from the registry, e.g. "26100.1150" (WMI lacks the UBR).</summary>
    [SupportedOSPlatform("windows")]
    private static string ReadBuild() {
        try {
            var build = ReadRegistryString("CurrentBuild");
            if (string.IsNullOrWhiteSpace(build))
                build = ReadRegistryString("CurrentBuildNumber");
            if (string.IsNullOrWhiteSpace(build))
                return "Unknown";

            var ubr = ReadRegistryInt("UBR");
            return ubr > 0 ? $"{build}.{ubr}" : build;
        } catch {
            return "Unknown";
        }
    }

    /// <summary>Reads the first non-empty string value of <paramref name="property"/> from a WMI query.</summary>
    [SupportedOSPlatform("windows")]
    private static string QueryString(string query, string property) {
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

    [SupportedOSPlatform("windows")]
    private static string ReadRegistryString(string valueName) {
        using var key = Registry.LocalMachine.OpenSubKey(CurrentVersionKey);
        return key?.GetValue(valueName) as string ?? "";
    }

    [SupportedOSPlatform("windows")]
    private static int ReadRegistryInt(string valueName) {
        using var key = Registry.LocalMachine.OpenSubKey(CurrentVersionKey);
        return key?.GetValue(valueName) is int i ? i : 0;
    }

    /// <summary>Joins two parts with a space, skipping blanks (e.g. vendor + version).</summary>
    private static string Join(string first, string second) {
        if (string.IsNullOrWhiteSpace(first))
            return second.Trim();
        if (string.IsNullOrWhiteSpace(second))
            return first.Trim();
        return $"{first.Trim()} {second.Trim()}";
    }
}
