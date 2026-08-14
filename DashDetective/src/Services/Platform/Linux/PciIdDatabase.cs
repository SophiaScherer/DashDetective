using System;
using System.Collections.Generic;
using System.Globalization;

namespace DashDetective.Services.Platform.Linux;

/// <summary>
/// Names a PCI device from the system's own <c>pci.ids</c> table — the database <c>lspci</c> reads, shipped
/// by the <c>hwdata</c> or <c>pciutils</c> package on every mainstream distribution.
///
/// <b>Why this exists.</b> The kernel identifies an adapter by its numeric PCI ids, never by its marketing
/// model, so <see cref="DrmCardFacts"/> can only compose "VMware vmwgfx (15ad:0405)" from what sysfs gives
/// it. That string names the right hardware but matches nothing in the bundled spec catalogue, whose keys
/// are model tokens — which is why the Hardware tab's Graphics card shows "—" for CUDA cores, boost clock
/// and bus on Linux. A real product name fills those rows with no further source.
///
/// <b>Scanned for the ids asked for, never loaded whole.</b> The file is ~1.5 MB and mostly subsystem
/// lines; a machine has one or two graphics adapters. One pass that keeps only the wanted pairs costs a
/// fraction of what holding the whole table would, and stops as soon as every pair is found.
///
/// Stateless and never throws: on a host with no <c>pci.ids</c> — a minimal container, a distro that does
/// not ship hwdata — <see cref="Read"/> yields <see cref="Empty"/>, every lookup misses, and each caller
/// keeps the name it composed for itself.
/// </summary>
internal sealed class PciIdDatabase {
    // Concatenated forward-slash literals, never Path.Combine — see IProcFileSystem. Debian and Ubuntu
    // carry hwdata's copy and pciutils' symlink; Fedora and Arch ship the first path only.
    private static readonly string[] CandidatePaths = [
        "/usr/share/hwdata/pci.ids",
        "/usr/share/misc/pci.ids",
        "/usr/share/pci.ids",
        "/var/lib/pciutils/pci.ids",
    ];

    /// <summary>Stands for "no vendor is open" while scanning. No PCI vendor id can reach it: the field is
    /// 16 bits wide.</summary>
    private const uint NoVendor = uint.MaxValue;

    private readonly Dictionary<uint, string> _vendors;
    private readonly Dictionary<ulong, string> _devices;

    private PciIdDatabase(Dictionary<uint, string> vendors, Dictionary<ulong, string> devices) {
        _vendors = vendors;
        _devices = devices;
    }

    /// <summary>The table for a host that ships no <c>pci.ids</c> — every lookup misses.</summary>
    internal static PciIdDatabase Empty { get; } = new([], []);

    /// <summary>The vendor's name, e.g. "NVIDIA Corporation"; "" when the table does not carry it.</summary>
    internal string Vendor(uint vendorId) => _vendors.GetValueOrDefault(vendorId, "");

    /// <summary>The device's name, e.g. "SVGA II Adapter"; "" when the table does not carry it.</summary>
    internal string Device(uint vendorId, uint deviceId) =>
        _devices.GetValueOrDefault(Pack(vendorId, deviceId), "");

    /// <summary>Reads the first table the host has, keeping only the wanted (vendor, device) pairs. An
    /// empty request, and a host with no table, both yield <see cref="Empty"/>.</summary>
    internal static PciIdDatabase Read(
        IProcFileSystem proc, IReadOnlyCollection<(uint Vendor, uint Device)> wanted) {
        if (wanted.Count == 0)
            return Empty;

        foreach (var path in CandidatePaths) {
            var lines = proc.ReadAllLines(path);
            if (lines.Count > 0)
                return Parse(lines, wanted);
        }

        return Empty;
    }

    /// <summary>
    /// One pass over the table. The format is indent-significant: a vendor sits at column 0
    /// (<c>15ad  VMware</c>), its devices one tab in (<c>\t0405  SVGA II Adapter</c>), and a device's
    /// subsystem variants two tabs in. Subsystem lines describe a board partner's build of the same chip,
    /// so they are skipped — a card must not be named after whoever assembled it.
    ///
    /// <b>The scan stops at the device-class section</b> that follows the last vendor. Its entries look
    /// like <c>C 03  Display controller</c>, and reading those two hex digits as a vendor id is how a
    /// parser starts attributing display-class names to real hardware. Pure; unit-tested.
    /// </summary>
    internal static PciIdDatabase Parse(
        IReadOnlyList<string> lines, IReadOnlyCollection<(uint Vendor, uint Device)> wanted) {
        var wantedVendors = new HashSet<uint>();
        var wantedDevices = new HashSet<ulong>();
        foreach (var (vendor, device) in wanted) {
            wantedVendors.Add(vendor);
            wantedDevices.Add(Pack(vendor, device));
        }

        var vendors = new Dictionary<uint, string>();
        var devices = new Dictionary<ulong, string>();
        var openVendor = NoVendor;

        foreach (var line in lines) {
            if (line.Length == 0 || line[0] == '#')
                continue;

            if (line[0] == 'C' && line.Length > 1 && line[1] == ' ')
                break;

            if (line[0] != '\t') {
                // A vendor nobody asked for closes the open one, so its devices are skipped outright.
                openVendor = NoVendor;
                if (ParseEntry(line, 0) is { } vendor && wantedVendors.Contains(vendor.Id)) {
                    openVendor = vendor.Id;
                    vendors[vendor.Id] = vendor.Name;
                }

                continue;
            }

            if (openVendor == NoVendor || line.Length < 2 || line[1] == '\t')
                continue;

            if (ParseEntry(line, 1) is not { } device)
                continue;

            var key = Pack(openVendor, device.Id);
            if (!wantedDevices.Contains(key))
                continue;

            devices[key] = device.Name;
            if (devices.Count == wantedDevices.Count)
                break;
        }

        return new PciIdDatabase(vendors, devices);
    }

    /// <summary>Splits one "<c>id  name</c>" entry at the given indent, or <c>null</c> when the line does
    /// not hold one. The id is bare hex here — unlike the sysfs id files, which carry an <c>0x</c> prefix.
    /// Pure; unit-tested.</summary>
    internal static (uint Id, string Name)? ParseEntry(string line, int indent) {
        if (line.Length <= indent)
            return null;

        var body = line.AsSpan(indent);
        var space = body.IndexOf(' ');
        if (space <= 0)
            return null;

        if (!uint.TryParse(body[..space], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var id))
            return null;

        var name = body[space..].Trim().ToString();
        return name.Length == 0 ? null : (id, name);
    }

    /// <summary>One key for a (vendor, device) pair. Shifted by a full 32 bits rather than 16 so a
    /// malformed id wider than the PCI field cannot collide with a real pair.</summary>
    private static ulong Pack(uint vendorId, uint deviceId) => ((ulong)vendorId << 32) | deviceId;
}
