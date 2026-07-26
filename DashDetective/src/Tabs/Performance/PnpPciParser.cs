using System;
using System.Globalization;

namespace DashDetective.Tabs.Performance;

/// <summary>
/// Extracts a PCI identity from a Windows PNP device string such as
/// <c>PCI\VEN_1002&amp;DEV_164E&amp;SUBSYS_7D731462&amp;REV_C7\4&amp;3207121D&amp;0&amp;0041</c>.
///
/// This is how AMD adapters are identified: ADL's own <c>iVendorID</c> field is unusable (see
/// <see cref="AdlInterop"/>), but its PNP string carries the same vendor/device/subsystem/revision ids DXGI
/// reports, so <see cref="GpuPciMatcher"/> can join on them. Pure; unit-tested.
/// </summary>
internal static class PnpPciParser {
    /// <summary>Parses a PNP device string into the packed form the vendor SDKs report, or <c>null</c> when it
    /// isn't a PCI device string or is missing the vendor/device ids. The trailing instance path is ignored;
    /// a missing SUBSYS or REV yields zero, which <see cref="GpuPciMatcher"/> treats as "not reported".</summary>
    internal static VendorPciId? Parse(string? pnpString) {
        if (string.IsNullOrEmpty(pnpString))
            return null;

        if (ReadHexField(pnpString, "VEN_", 4) is not { } vendor)
            return null;
        if (ReadHexField(pnpString, "DEV_", 4) is not { } device)
            return null;

        var subSys = ReadHexField(pnpString, "SUBSYS_", 8) ?? 0;
        var revision = ReadHexField(pnpString, "REV_", 2) ?? 0;
        return new VendorPciId(GpuPciMatcher.PackDeviceId(vendor, device), subSys, revision);
    }

    /// <summary>The PCI vendor id alone, for filtering an enumeration down to one vendor's adapters.</summary>
    internal static uint? ReadVendorId(string? pnpString) =>
        string.IsNullOrEmpty(pnpString) ? null : ReadHexField(pnpString, "VEN_", 4);

    /// <summary>Reads exactly <paramref name="digits"/> hex characters following <paramref name="token"/>, or
    /// <c>null</c> when the token is absent or the field is short/malformed.</summary>
    private static uint? ReadHexField(string source, string token, int digits) {
        var start = source.IndexOf(token, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
            return null;

        start += token.Length;
        if (start + digits > source.Length)
            return null;

        var slice = source.AsSpan(start, digits);
        return uint.TryParse(slice, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }
}
