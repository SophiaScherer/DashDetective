using DashDetective.Services.SystemMetrics;
using Xunit;

namespace DashDetective.Tests.Services.SystemMetrics;

/// <summary>Covers <see cref="PciLocation"/>: both platforms' notations read to the same bus/device/function
/// and are written as Task Manager's "Physical location" row, in decimal. Anything unparseable is null, so
/// the row is omitted rather than filled with a guess.</summary>
public class PciLocationTests {
    /// <summary>The value pci.sys actually stores (read on the dev box for the RTX 3060): a resource
    /// reference with the arguments appended, which is locale-independent.</summary>
    [Fact]
    public void FromWindows_ResourceReferenceWithArguments_ReadsTheTrailingTuple() {
        var location = PciLocation.FromWindows(
            @"@System32\drivers\pci.sys,#65536;PCI bus %1, device %2, function %3;(1,0,0)");

        Assert.Equal("PCI bus 1, device 0, function 0", location?.ToString());
    }

    [Fact]
    public void FromWindows_PlainText_IsReadToo() {
        Assert.Equal(new PciLocation(15, 0, 1), PciLocation.FromWindows("PCI bus 15, device 0, function 1"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Internal High Definition Audio Bus")]
    [InlineData(@"@System32\drivers\pci.sys,#65536;PCI bus %1, device %2, function %3;(1,0)")]
    [InlineData(@"@System32\drivers\pci.sys,#65536;PCI bus %1, device %2, function %3;(1,x,0)")]
    [InlineData("PCI bus 1, slot 0, function 0")]
    public void FromWindows_Unrecognized_IsNull(string? value) {
        Assert.Null(PciLocation.FromWindows(value));
    }

    /// <summary>A Linux address is hex, so bus 0x0f is "PCI bus 15" — the AMD iGPU on the dev box, which
    /// Windows reports as bus 15.</summary>
    [Theory]
    [InlineData("0000:01:00.0", "PCI bus 1, device 0, function 0")]
    [InlineData("0000:0f:00.0", "PCI bus 15, device 0, function 0")]
    [InlineData("0000:00:1f.3", "PCI bus 0, device 31, function 3")]
    public void FromLinux_PciAddress_FormatsInDecimal(string address, string expected) {
        Assert.Equal(expected, PciLocation.FromLinux(address)?.ToString());
    }

    /// <summary>A card with no PCI parent is keyed by its card name instead; that is not a location.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("card0")]
    [InlineData("0000:01:00")]
    [InlineData("0000:zz:00.0")]
    public void FromLinux_NotAPciAddress_IsNull(string? address) {
        Assert.Null(PciLocation.FromLinux(address));
    }
}
