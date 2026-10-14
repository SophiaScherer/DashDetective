using DashDetective.Tabs.Performance;
using System.Collections.Generic;

namespace DashDetective.Services.SystemMetrics;

/// <summary>One adapter's driver facts as the OS's device records hold them, keyed by the PCI identity its
/// PNP device string carries rather than by a LUID, which those records do not have.</summary>
internal sealed record GpuDriverRow(VendorPciId Pci, string? DriverVersion, string? PhysicalLocation);

/// <summary>
/// Joins the OS's per-device driver records to the DXGI adapters by PCI identity — the same join the
/// sensor readers use, through <see cref="GpuPciMatcher"/>. Pure, so it runs on every CI leg.
///
/// <b>Two cards of one model match the same rows</b>, and nothing here says which is which. The driver
/// version is still given when every match agrees, since it is one driver; the location is given only for
/// a single match, because pairing them by order could put each card at the other's slot.
/// </summary>
internal static class GpuDriverRows {
    /// <summary>A row for a PNP device string, or null when it does not name a PCI device. Blank values
    /// become null.</summary>
    internal static GpuDriverRow? From(string pnpDeviceId, string? driverVersion, string? locationInformation) =>
        PnpPciParser.Parse(pnpDeviceId) is { } pci
            ? new GpuDriverRow(pci, string.IsNullOrWhiteSpace(driverVersion) ? null : driverVersion.Trim(),
                               PciLocation.FromWindows(locationInformation)?.ToString())
            : null;

    /// <summary>The driver version and location for <paramref name="pci"/>, each null when no row matches
    /// or the matches disagree.</summary>
    internal static (string? DriverVersion, string? PhysicalLocation) For(
        GpuPciId pci, IReadOnlyList<GpuDriverRow> rows) {
        var candidates = new List<VendorPciId>(rows.Count);
        foreach (var row in rows)
            candidates.Add(row.Pci);

        var matches = new List<GpuDriverRow>();
        var claimed = new HashSet<int>();
        while (GpuPciMatcher.Match(pci, candidates, claimed) is { } index) {
            claimed.Add(index);
            matches.Add(rows[index]);
        }

        if (matches.Count == 0)
            return (null, null);

        var driver = matches[0].DriverVersion;
        var agreed = matches.TrueForAll(m => m.DriverVersion == driver);
        return (agreed ? driver : null, matches.Count == 1 ? matches[0].PhysicalLocation : null);
    }
}
