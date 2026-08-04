using DashDetective.Tabs.Hardware;
using System.Globalization;
using Xunit;

namespace DashDetective.Tests.Tabs.Hardware;

/// <summary>Covers <see cref="StorageSpecFormatter"/>: the Storage Devices card's subtitle and drive
/// rows. Capacities are decimal (marketing) units, so a drive sold as 2 TB reads "2 TB" rather than the
/// binary 1.8 TB — the one place the app deliberately diverges from <c>FileSizeFormatter</c>. Health is
/// worst-status-wins, and an unrecognised code reads "—" rather than claiming the drive is fine.</summary>
public class StorageSpecFormatterTests {
    [Theory]
    [InlineData(2_000_398_934_016UL, "2 TB")]       // a 2 TB SSD's real byte count
    [InlineData(4_000_787_030_016UL, "4 TB")]
    [InlineData(1_000_204_886_016UL, "1 TB")]
    [InlineData(512_110_190_592UL, "512.1 GB")]
    [InlineData(500_000_000_000UL, "500 GB")]
    [InlineData(0UL, "0 GB")]
    public void Capacity_UsesDecimalUnitsAndDropsATrailingZero(ulong bytes, string expected) {
        Assert.Equal(expected, StorageSpecFormatter.Capacity(bytes));
    }

    /// <summary>NVMe is a bus fact and wins over the media flag, so an NVMe SSD reads "NVMe".</summary>
    [Theory]
    [InlineData(4, 17, "NVMe")]
    [InlineData(3, 17, "NVMe")]
    [InlineData(4, 11, "SSD")]                      // BusType 11 = SATA
    [InlineData(3, 11, "HDD")]
    [InlineData(0, 0, "")]                          // neither known: the row shows size only
    [InlineData(5, 8, "")]
    public void TypeLabel_PrefersTheBusTypeOverTheMediaFlag(int media, int bus, string expected) {
        Assert.Equal(expected, StorageSpecFormatter.TypeLabel(media, bus));
    }

    [Theory]
    [InlineData(2_000_398_934_016UL, "NVMe", "2 TB NVMe")]
    [InlineData(500_000_000_000UL, "", "500 GB")]   // the Win32_DiskDrive fallback knows no type
    [InlineData(0UL, "SSD", "SSD")]                 // size unreadable: the type alone still says something
    [InlineData(0UL, "", "—")]
    public void DriveDetail_CombinesWhicheverFactsAreKnown(ulong bytes, string type, string expected) {
        Assert.Equal(expected, StorageSpecFormatter.DriveDetail(bytes, type));
    }

    [Theory]
    [InlineData(1, 1_000_204_886_016UL, "1 drive · 1 TB total")]
    [InlineData(2, 2_000_398_934_016UL, "2 drives · 2 TB total")]
    [InlineData(3, 4_000_787_030_016UL, "3 drives · 4 TB total")]
    public void Summary_AgreesInNumberWithTheDriveCount(int count, ulong totalBytes, string expected) {
        Assert.Equal(expected, StorageSpecFormatter.Summary(count, totalBytes));
    }

    [Theory]
    [InlineData(new[] { 0 }, "Good")]
    [InlineData(new[] { 0, 0, 0 }, "Good")]
    [InlineData(new[] { 0, 1 }, "Warning")]
    [InlineData(new[] { 0, 2 }, "Unhealthy")]
    [InlineData(new[] { 1, 2 }, "Unhealthy")]       // the worst status wins over the warning
    public void Health_ReportsTheWorstStatusAcrossTheDrives(int[] codes, string expected) {
        Assert.Equal(expected, StorageSpecFormatter.Health(codes));
    }

    [Theory]
    [InlineData(new int[0])]                        // nothing reported health
    [InlineData(new[] { 5 })]                       // an unrecognised code is not silently "Good"
    [InlineData(new[] { 0, 5 })]
    public void Health_WithNoUsableCodes_ReturnsPlaceholder(int[] codes) {
        Assert.Equal("—", StorageSpecFormatter.Health(codes));
    }

    /// <summary>The decimal separator is a period regardless of the ambient culture, matching the rest of
    /// the app's InvariantCulture formatting.</summary>
    [Fact]
    public void Capacity_UnderACommaDecimalCulture_StillUsesAPeriod() {
        var previous = CultureInfo.CurrentCulture;
        try {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            Assert.Equal("512.1 GB", StorageSpecFormatter.Capacity(512_110_190_592UL));
        } finally {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
