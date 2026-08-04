namespace DashDetective.Services.SystemMetrics;

/// <summary>What a physical disk is, as far as <c>MSFT_PhysicalDisk</c> can tell us.
/// <see cref="Unknown"/> covers a drive whose codes WMI didn't fill in — common on USB enclosures and
/// virtual disks — and callers render it as no type rather than guessing.</summary>
public enum DriveKind {
    Unknown,
    Hdd,
    Ssd,
    Nvme,
}

/// <summary>
/// Decodes <c>MSFT_PhysicalDisk</c>'s raw <c>MediaType</c>/<c>BusType</c> pair. The one place those codes
/// are interpreted: the Storage tab and the Hardware tab both label the same physical drives but word it
/// differently ("NVMe SSD" against the drive card, "NVMe" on the spec row), so they share the decoding
/// and keep their own wording. Previously each had its own copy of the numbers, which meant adding a bus
/// type fixed one tab and left the other disagreeing about the same drive.
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
}
