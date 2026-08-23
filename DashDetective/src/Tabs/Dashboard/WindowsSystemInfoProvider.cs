using DashDetective.Services.Diagnostics;
using DashDetective.Shared;
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
/// once at startup. Any failure yields <see cref="SystemStaticInfo.Unknown"/> rather than throwing;
/// each section also falls back independently so one dead source doesn't blank the others. The
/// platform check lives in <c>HardwareProviders.ForCurrentPlatform</c>, which is why this class carries
/// one <see cref="SupportedOSPlatformAttribute"/> instead of a guard and eight per-method attributes.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsSystemInfoProvider : ISystemInfoProvider {
    private const string CurrentVersionKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";

    public Task<SystemStaticInfo> GetAsync() => Task.Run(Read);

    private static SystemStaticInfo Read() {
        try {
            return new SystemStaticInfo(
                ReadOs(), Environment.MachineName, ReadBios(), ReadBuild(), ReadMotherboard());
        } catch (Exception e) {
            Log.Warn("SystemInfoProvider read failed", e);
            return SystemStaticInfo.Unknown;
        }
    }

    /// <summary>OS edition from WMI plus the registry feature-update label, e.g. "Windows 11 Pro 24H2".</summary>
    private static string ReadOs() {
        try {
            var caption = QueryString("SELECT Caption FROM Win32_OperatingSystem", "Caption");
            if (string.IsNullOrWhiteSpace(caption))
                caption = Placeholders.UnknownOs;
            // Win32_OperatingSystem reports "Microsoft Windows 11 Pro"; drop the prefix to match
            // the panel's compact style.
            else if (caption.StartsWith("Microsoft ", StringComparison.OrdinalIgnoreCase))
                caption = caption["Microsoft ".Length..];

            var display = ReadRegistryString("DisplayVersion");
            return string.IsNullOrWhiteSpace(display) ? caption : $"{caption} {display}";
        } catch {
            return Placeholders.UnknownOs;
        }
    }

    /// <summary>BIOS vendor and version from WMI, e.g. "American Megatrends Inc. F31d".</summary>
    private static string ReadBios() {
        try {
            var manufacturer = QueryString("SELECT Manufacturer, SMBIOSBIOSVersion FROM Win32_BIOS", "Manufacturer");
            var version = QueryString("SELECT Manufacturer, SMBIOSBIOSVersion FROM Win32_BIOS", "SMBIOSBIOSVersion");
            var text = Join(manufacturer, version);
            return string.IsNullOrWhiteSpace(text) ? Placeholders.UnknownBios : text;
        } catch {
            return Placeholders.UnknownBios;
        }
    }

    /// <summary>Motherboard vendor and product from WMI, e.g. "ASUSTeK COMPUTER INC. ROG STRIX Z790-E".</summary>
    private static string ReadMotherboard() {
        try {
            var manufacturer = QueryString("SELECT Manufacturer, Product FROM Win32_BaseBoard", "Manufacturer");
            var product = QueryString("SELECT Manufacturer, Product FROM Win32_BaseBoard", "Product");
            var text = Join(manufacturer, product);
            return string.IsNullOrWhiteSpace(text) ? Placeholders.UnknownMotherboard : text;
        } catch {
            return Placeholders.UnknownMotherboard;
        }
    }

    /// <summary>Full build number plus revision from the registry, e.g. "26100.1150" (WMI lacks the UBR).</summary>
    private static string ReadBuild() {
        try {
            var build = ReadRegistryString("CurrentBuild");
            if (string.IsNullOrWhiteSpace(build))
                build = ReadRegistryString("CurrentBuildNumber");
            if (string.IsNullOrWhiteSpace(build))
                return Placeholders.Unknown;

            var ubr = ReadRegistryInt("UBR");
            return ubr > 0 ? $"{build}.{ubr}" : build;
        } catch {
            return Placeholders.Unknown;
        }
    }

    /// <summary>Reads the first non-empty string value of <paramref name="property"/> from a WMI query.</summary>
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

    private static string ReadRegistryString(string valueName) {
        using var key = Registry.LocalMachine.OpenSubKey(CurrentVersionKey);
        return key?.GetValue(valueName) as string ?? "";
    }

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

/// <summary>The no-identity set — what the old <c>OperatingSystem.IsWindows()</c> guard returned.</summary>
internal sealed class UnsupportedSystemInfoProvider : ISystemInfoProvider {
    public Task<SystemStaticInfo> GetAsync() => Task.FromResult(SystemStaticInfo.Unknown);
}
