using System;
using System.Globalization;

namespace DashDetective.Services.Platform.Linux;

/// <summary>
/// Reads the board and firmware identity the kernel exposes as one-line files under
/// <c>/sys/class/dmi/id</c> — the rootless slice of what <c>dmidecode</c> reports. Two consumers share it:
/// the Dashboard's System Information panel (BIOS and Motherboard rows) and the Hardware tab's Motherboard
/// card, which is why it is a reader beside the <see cref="IProcFileSystem"/> seam rather than a private
/// helper on either provider.
///
/// <b>Only the world-readable keys are exposed, deliberately.</b> <c>product_uuid</c>, <c>board_serial</c>
/// and <c>product_serial</c> are mode 0400 and readable by root alone; naming them here as properties would
/// invite a caller to read them and get a silent "" on every normal machine. They are machine identifiers
/// this app has no use for, so the seam simply does not offer them.
///
/// Stateless and never throws: every field degrades to "" — a missing file, an unpopulated DMI table
/// (common in a VM) and a denied read are indistinguishable and all mean "not reported".
/// </summary>
internal sealed class DmiIdReader {
    // Concatenated forward-slash literals, never Path.Combine — see IProcFileSystem.
    private const string DmiRoot = "/sys/class/dmi/id";

    private readonly IProcFileSystem _proc;

    internal DmiIdReader(IProcFileSystem proc) => _proc = proc;

    /// <summary>Board vendor, e.g. "ASUSTeK COMPUTER INC." or "Oracle Corporation".</summary>
    internal string BoardVendor => Read("board_vendor");

    /// <summary>Board product name, e.g. "ROG STRIX Z790-E" or "VirtualBox".</summary>
    internal string BoardName => Read("board_name");

    /// <summary>Firmware vendor, e.g. "American Megatrends Inc." or "innotek GmbH".</summary>
    internal string BiosVendor => Read("bios_vendor");

    /// <summary>Firmware version string, e.g. "1203".</summary>
    internal string BiosVersion => Read("bios_version");

    /// <summary>Firmware release date in the DMI <c>MM/DD/YYYY</c> form.</summary>
    internal string BiosDate => Read("bios_date");

    /// <summary>System (chassis) vendor — the fallback when the board fields are unpopulated.</summary>
    internal string SysVendor => Read("sys_vendor");

    /// <summary>System product name — the fallback when the board fields are unpopulated.</summary>
    internal string ProductName => Read("product_name");

    /// <summary>Joins two parts with a space, skipping blanks (vendor + product, vendor + version). The
    /// Linux counterpart to <c>WmiRead.Join</c>, so both platforms compose the same display strings.</summary>
    internal static string Join(string first, string second) {
        if (string.IsNullOrWhiteSpace(first))
            return second.Trim();
        if (string.IsNullOrWhiteSpace(second))
            return first.Trim();

        return $"{first.Trim()} {second.Trim()}";
    }

    /// <summary>The year from a DMI date, which SMBIOS specifies as <c>MM/DD/YYYY</c>; 0 if unparseable.
    /// The counterpart to <c>WmiRead.DmtfYear</c>, which reads the same field out of WMI's very different
    /// <c>yyyymmdd…</c> encoding.</summary>
    internal static int Year(string dmiDate) {
        var lastSlash = dmiDate.LastIndexOf('/');
        if (lastSlash < 0)
            return 0;

        return int.TryParse(
            dmiDate.AsSpan(lastSlash + 1).Trim(),
            NumberStyles.None, CultureInfo.InvariantCulture, out var year)
            ? year
            : 0;
    }

    /// <summary>One DMI file, trimmed of the kernel's trailing newline; "" when absent or denied.</summary>
    private string Read(string key) => _proc.ReadAllText(DmiRoot + "/" + key)?.Trim() ?? "";
}
