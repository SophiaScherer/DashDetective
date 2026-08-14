using DashDetective.Services.Platform.Linux;
using DashDetective.Tests.Fakes;
using System.Collections.Generic;
using Xunit;

namespace DashDetective.Tests.Services.Platform.Linux;

/// <summary>
/// Covers <see cref="PciIdDatabase"/>: the indent-significant parse of the system <c>pci.ids</c> table,
/// the two boundaries a naive reader crosses (subsystem lines and the trailing device-class section), the
/// candidate-path search, and the degradation on a host that ships no table at all.
/// </summary>
public class PciIdDatabaseTests {
    private static readonly (uint Vendor, uint Device) Nvidia = (0x10deu, 0x2504u);
    private static readonly (uint Vendor, uint Device) Vmware = (0x15adu, 0x0405u);

    private static PciIdDatabase Read(params (uint Vendor, uint Device)[] wanted) =>
        PciIdDatabase.Read(new FakeProcFileSystem().WithPciIds(), wanted);

    [Fact]
    public void Read_ResolvesTheVendorAndDeviceName() {
        var db = Read(Vmware);

        Assert.Equal("VMware", db.Vendor(0x15adu));
        Assert.Equal("SVGA II Adapter", db.Device(0x15adu, 0x0405u));
    }

    [Fact]
    public void Read_ResolvesEveryWantedPair_AcrossVendors() {
        var db = Read(Nvidia, Vmware);

        Assert.Equal("GA106 [GeForce RTX 3060 Lite Hash Rate]", db.Device(0x10deu, 0x2504u));
        Assert.Equal("SVGA II Adapter", db.Device(0x15adu, 0x0405u));
    }

    /// <summary>A device the table lists under a <i>different</i> vendor must not answer for this one — the
    /// pair is the key, not the device id, and 16-bit device ids collide across vendors constantly.</summary>
    [Fact]
    public void Read_DoesNotMatchADeviceIdUnderTheWrongVendor() {
        Assert.Equal("", Read((0x10deu, 0x0405u)).Device(0x10deu, 0x0405u));
    }

    [Fact]
    public void Read_UnknownPair_MissesWithoutAffectingTheKnownOne() {
        var db = Read(Vmware, (0x1234u, 0x5678u));

        Assert.Equal("SVGA II Adapter", db.Device(0x15adu, 0x0405u));
        Assert.Equal("", db.Device(0x1234u, 0x5678u));
        Assert.Equal("", db.Vendor(0x1234u));
    }

    /// <summary>
    /// The device-class section trails every vendor and its entries are two hex digits deep
    /// (<c>C 03</c> → <c>00  VGA compatible controller</c>). A parse that does not stop there reads the
    /// class code as a vendor id, and every display adapter starts resolving to "VGA compatible
    /// controller".
    /// </summary>
    [Fact]
    public void Read_StopsAtTheDeviceClassSection() {
        Assert.Equal("", Read((0x03u, 0x00u)).Device(0x03u, 0x00u));
    }

    /// <summary>
    /// A subsystem line names a board partner's build of the same chip, so it must not overwrite the
    /// device. The guard is load-bearing rather than decorative: hex parsing allows leading whitespace, so
    /// a reader that strips only one tab parses the subvendor id as a device id perfectly happily.
    /// </summary>
    [Fact]
    public void Parse_SubsystemLine_NeverBecomesTheDeviceName() {
        var lines = new[] {
            "15ad  VMware",
            "\t0405  SVGA II Adapter",
            "\t\t0405 1234  Rebadged Board",
        };

        Assert.Equal(
            "SVGA II Adapter",
            PciIdDatabase.Parse(lines, [Vmware]).Device(0x15adu, 0x0405u));
    }

    /// <summary>A vendor line that does not parse must close the open vendor, or the following devices are
    /// filed under whichever vendor happened to precede it.</summary>
    [Fact]
    public void Parse_MalformedVendorLine_OrphansTheDevicesBeneathIt() {
        var lines = new[] {
            "15ad  VMware",
            "not-a-vendor",
            "\t0405  SVGA II Adapter",
        };

        Assert.Equal("", PciIdDatabase.Parse(lines, [Vmware]).Device(0x15adu, 0x0405u));
    }

    [Fact]
    public void Parse_SkipsCommentsAndBlankLines() {
        var lines = new[] {
            "# a comment",
            "",
            "15ad  VMware",
            "# another",
            "\t0405  SVGA II Adapter",
        };

        Assert.Equal(
            "SVGA II Adapter",
            PciIdDatabase.Parse(lines, [Vmware]).Device(0x15adu, 0x0405u));
    }

    /// <summary>Debian carries the table under <c>misc</c> as well as <c>hwdata</c>; a host with only the
    /// second path must still resolve.</summary>
    [Fact]
    public void Read_FindsTheTableAtAFallbackPath() {
        var proc = new FakeProcFileSystem().WithFile("/usr/share/misc/pci.ids", ProcFixtures.PciIds);

        Assert.Equal("SVGA II Adapter", PciIdDatabase.Read(proc, [Vmware]).Device(0x15adu, 0x0405u));
    }

    /// <summary>A minimal container ships no <c>pci.ids</c>. Every lookup misses and each caller keeps the
    /// name it composed for itself — the degradation, not a fault.</summary>
    [Fact]
    public void Read_NoTableOnTheHost_MissesEverything() {
        var db = PciIdDatabase.Read(new FakeProcFileSystem(), [Vmware]);

        Assert.Equal("", db.Device(0x15adu, 0x0405u));
        Assert.Equal("", db.Vendor(0x15adu));
    }

    /// <summary>Nothing wanted means nothing to read — the file is ~1.5 MB and a caller with no cards must
    /// not pay for it.</summary>
    [Fact]
    public void Read_NothingWanted_DoesNotTouchTheFile() {
        var proc = new FakeProcFileSystem().WithPciIds();

        PciIdDatabase.Read(proc, []);

        Assert.Empty(proc.Reads);
    }

    [Fact]
    public void Read_ReadsTheTableOnce_ForSeveralCards() {
        var proc = new FakeProcFileSystem().WithPciIds();

        PciIdDatabase.Read(proc, [Nvidia, Vmware]);

        Assert.Single(proc.Reads);
    }

    [Theory]
    [InlineData("1002  Advanced Micro Devices, Inc. [AMD/ATI]", 0, 0x1002u,
                "Advanced Micro Devices, Inc. [AMD/ATI]")]
    [InlineData("\t0405  SVGA II Adapter", 1, 0x0405u, "SVGA II Adapter")]
    [InlineData("\t73df  Navi 22 [Radeon RX 6700]", 1, 0x73dfu, "Navi 22 [Radeon RX 6700]")]
    public void ParseEntry_SplitsTheIdFromTheName(
        string line, int indent, uint expectedId, string expectedName) {
        var entry = PciIdDatabase.ParseEntry(line, indent);

        Assert.NotNull(entry);
        Assert.Equal(expectedId, entry.Value.Id);
        Assert.Equal(expectedName, entry.Value.Name);
    }

    /// <summary>Every shape that is not an entry, including the one a truncated file ends on.</summary>
    [Theory]
    [InlineData("", 0)]
    [InlineData("1002", 0)]
    [InlineData("  leading space", 0)]
    [InlineData("zzzz  Not Hex", 0)]
    [InlineData("1002  ", 0)]
    [InlineData("\t", 1)]
    public void ParseEntry_NonEntryLines_AreNull(string line, int indent) {
        Assert.Null(PciIdDatabase.ParseEntry(line, indent));
    }

    [Fact]
    public void Parse_EmptyTable_ResolvesNothing() {
        Assert.Equal("", PciIdDatabase.Parse([], [Vmware]).Device(0x15adu, 0x0405u));
    }

    [Fact]
    public void Empty_MissesEveryLookup() {
        Assert.Equal("", PciIdDatabase.Empty.Vendor(0x10deu));
        Assert.Equal("", PciIdDatabase.Empty.Device(0x10deu, 0x2504u));
    }

    /// <summary>Guards the shape the parser relies on: <see cref="IReadOnlyCollection{T}"/>, so a caller
    /// can pass the list it already built without copying it into an array.</summary>
    [Fact]
    public void Read_AcceptsAnyCollectionOfPairs() {
        var wanted = new List<(uint Vendor, uint Device)> { Vmware };

        Assert.Equal(
            "SVGA II Adapter",
            PciIdDatabase.Read(new FakeProcFileSystem().WithPciIds(), wanted).Device(0x15adu, 0x0405u));
    }
}
