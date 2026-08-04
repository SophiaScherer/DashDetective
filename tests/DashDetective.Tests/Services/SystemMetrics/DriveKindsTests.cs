using DashDetective.Services.SystemMetrics;
using DashDetective.Tabs.Hardware;
using Xunit;

namespace DashDetective.Tests.Services.SystemMetrics;

/// <summary>Covers <see cref="DriveKinds.FromStorageCodes"/>, the single decoding of
/// <c>MSFT_PhysicalDisk</c>'s MediaType/BusType pair. The Storage and Hardware tabs label the same drives
/// in different words but must agree on what each drive <i>is</i> — these codes used to be duplicated in
/// both, so adding a bus type could fix one tab and leave the other disagreeing.</summary>
public class DriveKindsTests {
    /// <summary>NVMe is a bus fact, so it outranks whatever the media flag says — including the common
    /// case of an NVMe drive that also reports MediaType 4 (SSD).</summary>
    [Theory]
    [InlineData(4, 17)]     // NVMe SSD, the usual pairing
    [InlineData(3, 17)]     // a mislabelled media flag doesn't demote it
    [InlineData(0, 17)]     // no media flag at all
    public void FromStorageCodes_WithTheNvmeBus_ReportsNvme(int mediaType, int busType) {
        Assert.Equal(DriveKind.Nvme, DriveKinds.FromStorageCodes(mediaType, busType));
    }

    [Theory]
    [InlineData(4, 11, DriveKind.Ssd)]      // BusType 11 = SATA
    [InlineData(3, 11, DriveKind.Hdd)]
    [InlineData(4, 7, DriveKind.Ssd)]       // BusType 7 = USB
    [InlineData(3, 8, DriveKind.Hdd)]       // BusType 8 = RAID
    public void FromStorageCodes_OffTheNvmeBus_FallsBackToTheMediaFlag(
        int mediaType, int busType, DriveKind expected) {
        Assert.Equal(expected, DriveKinds.FromStorageCodes(mediaType, busType));
    }

    /// <summary>Unfilled codes stay Unknown rather than being guessed into a type — both tabs render that
    /// as no label rather than a wrong one.</summary>
    [Theory]
    [InlineData(0, 0)]
    [InlineData(5, 8)]      // MediaType 5 = SCM, which neither tab has wording for
    [InlineData(0, 11)]
    public void FromStorageCodes_WithUnfilledCodes_ReportsUnknown(int mediaType, int busType) {
        Assert.Equal(DriveKind.Unknown, DriveKinds.FromStorageCodes(mediaType, busType));
    }

    /// <summary>The two tabs' wordings stay deliberately different, but never disagree about the kind:
    /// wherever the Hardware spec row says "NVMe", the Storage card says "NVMe SSD" — and where one says
    /// nothing, so does the other.</summary>
    [Theory]
    [InlineData(4, 17, "NVMe")]
    [InlineData(4, 11, "SSD")]
    [InlineData(3, 11, "HDD")]
    [InlineData(0, 0, "")]
    public void HardwareSpecRow_AgreesWithTheDecodedKind(int mediaType, int busType, string expected) {
        Assert.Equal(expected, StorageSpecFormatter.TypeLabel(mediaType, busType));
    }
}
