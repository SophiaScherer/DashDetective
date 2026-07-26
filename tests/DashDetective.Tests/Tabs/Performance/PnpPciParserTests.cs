using DashDetective.Tabs.Performance;
using Xunit;

namespace DashDetective.Tests.Tabs.Performance;

/// <summary>Covers <see cref="PnpPciParser"/>: pulling a PCI identity out of a Windows PNP device string, which
/// is how AMD adapters are identified (ADL's own vendor field reports nonsense). The parsed ids must line up
/// with what DXGI reports for the same adapter, so a sensor reading lands on the right row.</summary>
public class PnpPciParserTests {
    // The two adapters in the development machine, exactly as ADL reports their PNP strings.
    private const string RadeonPnp = @"PCI\VEN_1002&DEV_164E&SUBSYS_7D731462&REV_C7\4&3207121D&0&0041";
    private const string NvidiaPnp = @"PCI\VEN_10DE&DEV_2504&SUBSYS_397D1462&REV_A1\4&2D908A1A&0&0009";

    /// <summary>The parsed ids must equal what DXGI reports for the same board, since the two are joined.</summary>
    [Fact]
    public void Parse_RadeonPnpString_MatchesTheDxgiIdentity() {
        var parsed = PnpPciParser.Parse(RadeonPnp);

        Assert.NotNull(parsed);
        Assert.Equal(0x164E1002u, parsed.Value.PackedDeviceId);
        Assert.Equal(0x7D731462u, parsed.Value.SubSysId);
        Assert.Equal(0xC7u, parsed.Value.Revision);
    }

    [Fact]
    public void Parse_NvidiaPnpString_MatchesTheDxgiIdentity() {
        var parsed = PnpPciParser.Parse(NvidiaPnp);

        Assert.NotNull(parsed);
        Assert.Equal(0x250410DEu, parsed.Value.PackedDeviceId);
        Assert.Equal(0x397D1462u, parsed.Value.SubSysId);
        Assert.Equal(0xA1u, parsed.Value.Revision);
    }

    /// <summary>ADL lists one physical GPU once per display output, differing only in the trailing instance
    /// path — every one of them must parse to the same identity so they de-duplicate.</summary>
    [Theory]
    [InlineData(@"PCI\VEN_1002&DEV_164E&SUBSYS_7D731462&REV_C7\4&3207121D&0&0041")]
    [InlineData(@"PCI\VEN_1002&DEV_164E&SUBSYS_7D731462&REV_C7\4&3207121D&0&0041&02")]
    [InlineData(@"PCI\VEN_1002&DEV_164E&SUBSYS_7D731462&REV_C7\4&3207121D&0&0041&05")]
    public void Parse_SameGpuUnderDifferentDisplayOutputs_YieldsOneIdentity(string pnp) {
        Assert.Equal(PnpPciParser.Parse(RadeonPnp), PnpPciParser.Parse(pnp));
    }

    /// <summary>Lower-case hex appears in some PNP strings and must parse identically.</summary>
    [Fact]
    public void Parse_LowerCaseHex_IsAccepted() {
        Assert.Equal(PnpPciParser.Parse(RadeonPnp),
                     PnpPciParser.Parse(@"pci\ven_1002&dev_164e&subsys_7d731462&rev_c7\4&3207121d&0&0041"));
    }

    /// <summary>A device string with no subsystem or revision still identifies the board; the missing fields
    /// read zero, which the matcher treats as "not reported" rather than a mismatch.</summary>
    [Fact]
    public void Parse_MissingSubsystemAndRevision_LeavesThemZero() {
        var parsed = PnpPciParser.Parse(@"PCI\VEN_1002&DEV_164E\4&3207121D&0&0041");

        Assert.NotNull(parsed);
        Assert.Equal(0x164E1002u, parsed.Value.PackedDeviceId);
        Assert.Equal(0u, parsed.Value.SubSysId);
        Assert.Equal(0u, parsed.Value.Revision);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(@"ROOT\DISPLAY\0000")]                  // the Parsec virtual adapter — not a PCI device
    [InlineData(@"PCI\DEV_164E&SUBSYS_7D731462")]       // no vendor
    [InlineData(@"PCI\VEN_1002&SUBSYS_7D731462")]       // no device
    [InlineData(@"PCI\VEN_1002&DEV_16")]                // device field truncated
    [InlineData(@"PCI\VEN_ZZZZ&DEV_164E")]              // not hex
    public void Parse_NotAUsablePciString_ReturnsNull(string? pnp) {
        Assert.Null(PnpPciParser.Parse(pnp));
    }

    [Fact]
    public void ReadVendorId_PciString_ReturnsTheVendorAlone() {
        Assert.Equal(0x1002u, PnpPciParser.ReadVendorId(RadeonPnp));
        Assert.Equal(0x10DEu, PnpPciParser.ReadVendorId(NvidiaPnp));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(@"ROOT\DISPLAY\0000")]
    public void ReadVendorId_NotAPciString_ReturnsNull(string? pnp) {
        Assert.Null(PnpPciParser.ReadVendorId(pnp));
    }
}
