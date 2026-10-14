using System;
using System.Globalization;

namespace DashDetective.Services.SystemMetrics;

/// <summary>
/// An adapter's place on the PCI bus, read from either platform's own notation and written the way Task
/// Manager's "Physical location" row words it: "PCI bus 1, device 0, function 0", in decimal. Pure, so both
/// parsers run on every CI leg.
/// </summary>
internal readonly record struct PciLocation(int Bus, int Device, int Function) {
    /// <summary>Task Manager's wording. Decimal, as Windows writes it, so bus 0x0f reads "PCI bus 15".</summary>
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"PCI bus {Bus}, device {Device}, function {Function}");

    /// <summary>
    /// Parses a device's <c>LocationInformation</c> registry value. pci.sys stores it as a resource
    /// reference with its arguments appended, <c>@System32\drivers\pci.sys,#65536;PCI bus %1, device %2,
    /// function %3;(1,0,0)</c>, so the trailing tuple is read rather than the localized text. An older
    /// plain "PCI bus 1, device 0, function 0" is read too. Anything else is null.
    /// </summary>
    public static PciLocation? FromWindows(string? locationInformation) {
        if (string.IsNullOrWhiteSpace(locationInformation))
            return null;

        var text = locationInformation.Trim();
        if (text.EndsWith(')') && text.LastIndexOf('(') is var open and >= 0) {
            var args = text[(open + 1)..^1].Split(',', StringSplitOptions.TrimEntries);
            return args.Length == 3 ? Build(args[0], args[1], args[2], NumberStyles.None) : null;
        }

        const string prefix = "PCI bus ";
        if (!text.StartsWith(prefix, StringComparison.Ordinal))
            return null;

        var parts = text[prefix.Length..].Split(',', StringSplitOptions.TrimEntries);
        return parts.Length == 3
               && parts[1].StartsWith("device ", StringComparison.Ordinal)
               && parts[2].StartsWith("function ", StringComparison.Ordinal)
            ? Build(parts[0], parts[1]["device ".Length..], parts[2]["function ".Length..], NumberStyles.None)
            : null;
    }

    /// <summary>Parses a Linux PCI address, <c>0000:01:00.0</c> (domain:bus:device.function, hex), as
    /// <c>/sys/class/drm/cardN/device</c> resolves to. Null for anything that is not one, such as the card
    /// name a device with no PCI parent is keyed by instead.</summary>
    public static PciLocation? FromLinux(string? pciAddress) {
        if (string.IsNullOrEmpty(pciAddress))
            return null;

        var fields = pciAddress.Split(':');
        if (fields.Length != 3)
            return null;

        var slot = fields[2].Split('.');
        return slot.Length == 2 ? Build(fields[1], slot[0], slot[1], NumberStyles.AllowHexSpecifier) : null;
    }

    private static PciLocation? Build(string bus, string device, string function, NumberStyles style) =>
        int.TryParse(bus, style, CultureInfo.InvariantCulture, out var b)
        && int.TryParse(device, style, CultureInfo.InvariantCulture, out var d)
        && int.TryParse(function, style, CultureInfo.InvariantCulture, out var f)
            ? new PciLocation(b, d, f)
            : null;
}
