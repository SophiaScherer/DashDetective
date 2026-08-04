using System;
using System.Management;
using System.Runtime.Versioning;

namespace DashDetective.Tabs.Hardware;

/// <summary>
/// The WMI boilerplate the Hardware tab's per-card providers share: enumerating a query with everything
/// disposed, and converting the loosely-typed property values WMI hands back. Deliberately transparent —
/// it does not catch, so each provider keeps its own soft-fail to its <c>.Unknown</c> record.
///
/// Tab-local for now, per the repo convention that a helper only moves to <c>src/Services</c> once a
/// second tab needs the same reading. <c>WindowsSystemInfoProvider</c> and
/// <c>WindowsPhysicalDiskProvider</c> carry their own copies of some of this; folding them in is a
/// separate, wider change.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class WmiRead {
    /// <summary>Runs <paramref name="query"/> against the default scope, invoking <paramref name="read"/>
    /// once per row.</summary>
    public static void ForEach(string query, Action<ManagementBaseObject> read) {
        using var searcher = new ManagementObjectSearcher(query);
        Enumerate(searcher, read);
    }

    /// <summary>Runs <paramref name="query"/> against a non-default namespace — the storage providers need
    /// <c>root\Microsoft\Windows\Storage</c>, which is not where WMI looks by default.</summary>
    public static void ForEach(string scopePath, string query, Action<ManagementBaseObject> read) {
        using var searcher = new ManagementObjectSearcher(
            new ManagementScope(scopePath), new ObjectQuery(query));
        Enumerate(searcher, read);
    }

    /// <summary>A trimmed string property, or "" when absent or blank.</summary>
    public static string Text(ManagementBaseObject obj, string property) =>
        obj[property] is string s && !string.IsNullOrWhiteSpace(s) ? s.Trim() : "";

    /// <summary>Joins two parts with a space, skipping blanks (e.g. vendor + product).</summary>
    public static string Join(string first, string second) {
        if (string.IsNullOrWhiteSpace(first)) return second.Trim();
        if (string.IsNullOrWhiteSpace(second)) return first.Trim();
        return $"{first.Trim()} {second.Trim()}";
    }

    /// <summary>Extracts the year from a WMI/DMTF datetime (leading "yyyy…"); 0 if unparseable.</summary>
    public static int DmtfYear(string dmtf) =>
        dmtf.Length >= 4 && int.TryParse(dmtf[..4], out var year) ? year : 0;

    public static int ToInt(object? value) => value is null ? 0 : Convert.ToInt32(value);

    public static ulong ToUInt64(object? value) => value is null ? 0 : Convert.ToUInt64(value);

    private static void Enumerate(ManagementObjectSearcher searcher, Action<ManagementBaseObject> read) {
        using var results = searcher.Get();
        foreach (var obj in results) {
            using (obj)
                read(obj);
        }
    }
}
