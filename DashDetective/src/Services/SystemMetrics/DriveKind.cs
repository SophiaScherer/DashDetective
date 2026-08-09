using System;

namespace DashDetective.Services.SystemMetrics;

/// <summary>What a physical disk is, as far as the platform can tell us.
/// <see cref="Unknown"/> covers a drive whose codes WMI didn't fill in — common on USB enclosures and
/// virtual disks — and callers render it as no type rather than guessing.</summary>
public enum DriveKind {
    Unknown,
    Hdd,
    Ssd,
    Nvme,
}

/// <summary>
/// Decodes each platform's raw drive-type facts into one <see cref="DriveKind"/>: WMI's
/// <c>MediaType</c>/<c>BusType</c> pair on Windows, sysfs's <c>queue/rotational</c> flag on Linux. The one
/// place those are interpreted: the Storage tab and the Hardware tab both label the same physical drives
/// but word it differently ("NVMe SSD" against the drive card, "NVMe" on the spec row), so they share the
/// decoding and keep their own wording. Previously each had its own copy of the numbers, which meant adding
/// a bus type fixed one tab and left the other disagreeing about the same drive.
/// </summary>
public static class DriveKinds {
    // BusType 17 = NVMe; MediaType 4 = SSD, 3 = HDD.
    private const int BusTypeNvme = 17;
    private const int MediaTypeSsd = 4;
    private const int MediaTypeHdd = 3;

    /// <summary>NVMe is a bus fact and wins over the media flag, so an NVMe SSD reads as
    /// <see cref="DriveKind.Nvme"/> rather than <see cref="DriveKind.Ssd"/>.</summary>
    public static DriveKind FromStorageCodes(int mediaType, int busType) {
        if (busType == BusTypeNvme) return DriveKind.Nvme;
        if (mediaType == MediaTypeSsd) return DriveKind.Ssd;
        if (mediaType == MediaTypeHdd) return DriveKind.Hdd;
        return DriveKind.Unknown;
    }

    /// <summary>
    /// The sysfs reading: <c>queue/rotational</c> tells platter from solid state, and the bus has to come
    /// from the device name because Linux exposes no bus-type field beside it — an NVMe namespace is always
    /// <c>nvmeXnY</c>. Mirrors <see cref="FromStorageCodes"/>'s precedence, so both platforms call an NVMe
    /// drive NVMe rather than SSD.
    /// </summary>
    public static DriveKind FromSysBlock(string deviceName, bool isRotational) {
        if (deviceName.StartsWith("nvme", StringComparison.Ordinal)) return DriveKind.Nvme;
        return isRotational ? DriveKind.Hdd : DriveKind.Ssd;
    }

    /// <summary>The Storage tab's drive-card wording — spelled out ("NVMe SSD") since it heads its own
    /// card, unlike the Hardware tab's terser spec row in <c>StorageSpecFormatter</c>. "" when the kind is
    /// unknown. Lives here because both platform providers fill the same
    /// <c>PhysicalDiskInfo.TypeLabel</c>.</summary>
    public static string CardLabel(DriveKind kind) => kind switch {
        DriveKind.Nvme => "NVMe SSD",
        DriveKind.Ssd => "SSD",
        DriveKind.Hdd => "HDD",
        _ => "",
    };
}
