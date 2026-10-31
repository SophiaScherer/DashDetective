using DashDetective.Services.SystemMetrics;
using Xunit;

namespace DashDetective.Tests.Services.SystemMetrics;

/// <summary>Covers <see cref="GpuDriverRows"/>: the OS's device records join to a DXGI adapter by PCI
/// identity, an unmatched adapter gets nulls, and two cards of one model never have their locations paired
/// by guesswork.</summary>
public class GpuDriverRowsTests {
    private const string Nvidia = @"PCI\VEN_10DE&DEV_2504&SUBSYS_397D1462&REV_A1\4&2D908A1A&0&0009";
    private const string Amd = @"PCI\VEN_1002&DEV_164E&SUBSYS_7D731462&REV_C7\4&3207121D&0&0041";
    private const string Location = @"@System32\drivers\pci.sys,#65536;PCI bus %1, device %2, function %3;(1,0,0)";

    private static readonly GpuPciId Rtx3060 = new(0x10DE, 0x2504, 0x397D1462, 0xA1);

    [Fact]
    public void From_PciDevice_ParsesTheIdsAndFormatsTheLocation() {
        var row = GpuDriverRows.From(Nvidia, " 32.0.16.1062 ", Location);

        Assert.NotNull(row);
        Assert.Equal("32.0.16.1062", row.DriverVersion);
        Assert.Equal("PCI bus 1, device 0, function 0", row.PhysicalLocation);
    }

    [Fact]
    public void From_BlankValues_AreNull() {
        var row = GpuDriverRows.From(Nvidia, "", null);

        Assert.NotNull(row);
        Assert.Null(row.DriverVersion);
        Assert.Null(row.PhysicalLocation);
    }

    [Fact]
    public void From_NotAPciDevice_IsNull() {
        Assert.Null(GpuDriverRows.From(@"ROOT\Parsec\VDA\0000", "1.0", null));
    }

    [Fact]
    public void For_MatchingRow_GivesItsDriverAndLocation() {
        var rows = new[] {
            GpuDriverRows.From(Amd, "32.0.12019.1028", null)!,
            GpuDriverRows.From(Nvidia, "32.0.16.1062", Location)!,
        };

        Assert.Equal(("32.0.16.1062", "PCI bus 1, device 0, function 0"), GpuDriverRows.For(Rtx3060, rows));
    }

    [Fact]
    public void For_NoMatchingRow_IsNullForBoth() {
        var rows = new[] { GpuDriverRows.From(Amd, "32.0.12019.1028", null)! };

        Assert.Equal((null, null), GpuDriverRows.For(Rtx3060, rows));
    }

    /// <summary>Two identical cards: one driver serves both, so the version stands, but which slot is which
    /// cannot be told, so neither gets a location.</summary>
    [Fact]
    public void For_TwoCardsOfOneModel_KeepsTheSharedDriverAndDropsTheLocation() {
        var rows = new[] {
            GpuDriverRows.From(Nvidia, "32.0.16.1062", Location)!,
            GpuDriverRows.From(Nvidia, "32.0.16.1062", Location.Replace("(1,0,0)", "(2,0,0)"))!,
        };

        Assert.Equal(("32.0.16.1062", null), GpuDriverRows.For(Rtx3060, rows));
    }

    [Fact]
    public void For_MatchesDisagreeOnTheDriver_IsNull() {
        var rows = new[] {
            GpuDriverRows.From(Nvidia, "32.0.16.1062", Location)!,
            GpuDriverRows.From(Nvidia, "31.0.15.5222", Location)!,
        };

        Assert.Null(GpuDriverRows.For(Rtx3060, rows).DriverVersion);
    }
}
