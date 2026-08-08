using DashDetective.Services.Platform.Linux;
using DashDetective.Tests.Fakes;
using Xunit;

namespace DashDetective.Tests.Services.Platform.Linux;

/// <summary>Covers <see cref="DmiIdReader"/>: the trailing newline every sysfs one-liner carries, the
/// degradation when the DMI table is unpopulated or a file is denied, and the <c>MM/DD/YYYY</c> date form
/// — which is not the encoding WMI reports the same field in.</summary>
public class DmiIdReaderTests {
    private static DmiIdReader VirtualBox() => new(new FakeProcFileSystem().WithVirtualBoxDmi());

    private static DmiIdReader Empty() => new(new FakeProcFileSystem());

    /// <summary>The kernel terminates every one of these files with a newline, which would otherwise land
    /// in the middle of a joined display string.</summary>
    [Fact]
    public void Fields_AreTrimmedOfTheTrailingNewline() {
        Assert.Equal("Oracle Corporation", VirtualBox().BoardVendor);
        Assert.Equal("VirtualBox", VirtualBox().BoardName);
        Assert.Equal("innotek GmbH", VirtualBox().BiosVendor);
    }

    /// <summary>An unpopulated DMI table — the normal case on ARM boards and in some hypervisors — reads
    /// as "not reported" for every field rather than throwing or yielding null.</summary>
    [Fact]
    public void Fields_WithNoDmiTable_AreEmpty() {
        Assert.Equal("", Empty().BoardVendor);
        Assert.Equal("", Empty().BiosVersion);
        Assert.Equal("", Empty().ProductName);
    }

    [Fact]
    public void Join_SkipsABlankSide() {
        Assert.Equal("Oracle Corporation", DmiIdReader.Join("Oracle Corporation", ""));
        Assert.Equal("VirtualBox", DmiIdReader.Join("   ", "VirtualBox"));
        Assert.Equal("", DmiIdReader.Join("", ""));
    }

    [Fact]
    public void Join_CombinesBothSidesWithOneSpace() =>
        Assert.Equal("Oracle Corporation VirtualBox", DmiIdReader.Join("Oracle Corporation ", " VirtualBox"));

    /// <summary>SMBIOS writes the date as <c>MM/DD/YYYY</c>, so the year is the <b>last</b> field — the
    /// opposite end from WMI's <c>yyyymmdd…</c>, which <c>WmiRead.DmtfYear</c> reads off the front. Taking
    /// the first four characters here would report the month.</summary>
    [Fact]
    public void Year_ReadsTheLastFieldOfAnSmbiosDate() => Assert.Equal(2006, DmiIdReader.Year("12/01/2006"));

    [Fact]
    public void Year_WhenUnparseable_IsZero() {
        Assert.Equal(0, DmiIdReader.Year(""));
        Assert.Equal(0, DmiIdReader.Year("2006"));
        Assert.Equal(0, DmiIdReader.Year("12/01/not-a-year"));
    }
}
