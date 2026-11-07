using System;
using System.Collections.Generic;
using System.Globalization;

namespace DashDetective.Services.Platform.Linux;

/// <summary>
/// Names a PCI device from <c>pci.ids</c>, since the kernel gives only numeric ids and the Graphics card's spec lookup needs a product name.
/// </summary>
internal sealed class PciIdDatabase {
    // Forward-slash literals, never Path.Combine (see IProcFileSystem); distros ship the table at different paths.
    private static readonly string[] CandidatePaths = [
        "/usr/share/hwdata/pci.ids",
        "/usr/share/misc/pci.ids",
        "/usr/share/pci.ids",
        "/var/lib/pciutils/pci.ids",
    ];

    /// <summary>"No vendor is open"; no 16-bit PCI vendor id can reach it.</summary>
    private const uint NoVendor = uint.MaxValue;

    private readonly Dictionary<uint, string> _vendors;
    private readonly Dictionary<ulong, string> _devices;

    private PciIdDatabase(Dictionary<uint, string> vendors, Dictionary<ulong, string> devices) {
        _vendors = vendors;
        _devices = devices;
    }

    /// <summary>The table for a host that ships no <c>pci.ids</c> — every lookup misses.</summary>
    internal static PciIdDatabase Empty { get; } = new([], []);

    /// <summary>The vendor's name ("NVIDIA Corporation"), or "" when the table lacks it.</summary>
    internal string Vendor(uint vendorId) => _vendors.GetValueOrDefault(vendorId, "");

    /// <summary>The device's name ("SVGA II Adapter"), or "" when the table lacks it.</summary>
    internal string Device(uint vendorId, uint deviceId) =>
        _devices.GetValueOrDefault(Pack(vendorId, deviceId), "");

    /// <summary>Reads the first table the host has, keeping only the wanted pairs; no request or no table yields <see cref="Empty"/>.</summary>
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

    /// <summary>Stops at the device-class section, whose <c>C 03</c> entries would parse as vendor ids; subsystem lines are skipped.</summary>
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

    /// <summary>Splits one "<c>id  name</c>" entry at the given indent, or <c>null</c>; the id is bare hex, unlike sysfs's <c>0x</c>-prefixed ones.</summary>
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

    /// <summary>Shifted by 32 bits rather than 16 so a malformed id wider than the PCI field cannot collide with a real pair.</summary>
    private static ulong Pack(uint vendorId, uint deviceId) => ((ulong)vendorId << 32) | deviceId;
}
