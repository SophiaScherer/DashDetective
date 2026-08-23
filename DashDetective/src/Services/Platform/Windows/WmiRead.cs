using System;
using System.Management;
using System.Runtime.Versioning;

namespace DashDetective.Services.Platform.Windows;

/// <summary>
/// The WMI boilerplate every Windows provider shares: enumerating a query with everything disposed, and
/// converting the loosely-typed property values WMI hands back. <see cref="ForEach"/> is deliberately
/// transparent — it does not catch, so each provider keeps its own soft-fail to its <c>.Unknown</c>
/// record.
///
/// The Linux counterpart is <c>Services/Platform/Linux</c>; this is its sibling, and the two are meant
/// to be read together — <c>DmiIdReader.Join</c> and <c>DmiIdReader</c>'s date handling exist so both
/// platforms compose the same display strings.
/// </summary>
/// <remarks>
/// Promoted here from <c>Tabs/Hardware/Providers</c>: five other files across the Dashboard, Storage and
/// the metrics services had grown their own copies of the value converters and <see cref="Join"/>, which
/// is the second-consumer bar the promotion rule waits for. The copies had already drifted — see the
/// note on <see cref="ToInt"/>.
/// </remarks>
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

    /// <summary>
    /// A WMI integer property, or <c>0</c> when absent or unconvertible.
    ///
    /// <para>The conversion is guarded, unlike the rest of this class. The five copies this replaced had
    /// split into two behaviours: the Dashboard's let a malformed value throw, which its provider's
    /// outer catch turned into a wholly <c>Unknown</c> card, while the storage ones returned 0 and kept
    /// the rest of the row. The second is what the architecture asks for — each field falls back on its
    /// own, so one dead property never blanks the panel around it — so that is the behaviour here.</para>
    ///
    /// <para>Reporting an unreadable count as <c>0</c> is safe only because <c>0</c> is not a real
    /// reading for anything this parses: a machine has at least one core, a volume a non-zero size. A
    /// field where <c>0</c> IS meaningful must not use this.</para>
    /// </summary>
    public static int ToInt(object? value) {
        try {
            return value is null ? 0 : Convert.ToInt32(value);
        } catch (Exception e) when (e is InvalidCastException or FormatException or OverflowException) {
            return 0;
        }
    }

    /// <summary>A WMI unsigned property, or <c>0</c> when absent or unconvertible — see
    /// <see cref="ToInt"/> for why 0 is the safe answer here.</summary>
    public static ulong ToUInt64(object? value) {
        try {
            return value is null ? 0 : Convert.ToUInt64(value);
        } catch (Exception e) when (e is InvalidCastException or FormatException or OverflowException) {
            return 0;
        }
    }

    private static void Enumerate(ManagementObjectSearcher searcher, Action<ManagementBaseObject> read) {
        using var results = searcher.Get();
        foreach (var obj in results) {
            using (obj)
                read(obj);
        }
    }
}
