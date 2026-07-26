using DashDetective.Services.SystemMetrics;
using DashDetective.Tabs.Performance;
using System.Collections.Generic;
using Xunit;

namespace DashDetective.Tests.Tabs.Performance;

/// <summary>Covers <see cref="GpuPciMatcher"/>: packing a DXGI vendor/device pair into the combined id the
/// vendor SDKs report, and joining a DXGI adapter to the matching vendor-enumerated one — including the
/// tie-break that keeps two identical cards from both resolving to the first.</summary>
public class GpuPciMatcherTests {
    // The RTX 3060 in the development machine, as DXGI reports it.
    private static readonly GpuPciId Rtx3060 = new(0x10DE, 0x2504, 0x397D1462, 0xA1);

    [Theory]
    [InlineData(0x10DEu, 0x2504u, 0x250410DEu)]   // NVIDIA RTX 3060, verified against nvidia-smi
    [InlineData(0x1002u, 0x164Eu, 0x164E1002u)]   // AMD Raphael iGPU
    [InlineData(0x8086u, 0x0000u, 0x00008086u)]
    public void PackDeviceId_VendorAndDevice_PacksDeviceIntoTheHighWord(uint vendor, uint device, uint expected) {
        Assert.Equal(expected, GpuPciMatcher.PackDeviceId(vendor, device));
    }

    /// <summary>A vendor id wider than 16 bits must not bleed into the device half.</summary>
    [Fact]
    public void PackDeviceId_OversizedVendorId_IsMaskedToSixteenBits() {
        Assert.Equal(0x250410DEu, GpuPciMatcher.PackDeviceId(0xFFFF10DE, 0x2504));
    }

    [Fact]
    public void Match_ExactVendorReport_ReturnsThatIndex() {
        var candidates = new List<VendorPciId> {
            new(0x164E1002, 0x7D731462, 0xC7),
            new(0x250410DE, 0x397D1462, 0xA1),
        };
        Assert.Equal(1, GpuPciMatcher.Match(Rtx3060, candidates));
    }

    [Fact]
    public void Match_NoVendorReportsThatAdapter_ReturnsNull() {
        var candidates = new List<VendorPciId> { new(0x164E1002, 0x7D731462, 0xC7) };
        Assert.Null(GpuPciMatcher.Match(Rtx3060, candidates));
    }

    [Fact]
    public void Match_EmptyCandidates_ReturnsNull() {
        Assert.Null(GpuPciMatcher.Match(Rtx3060, new List<VendorPciId>()));
    }

    /// <summary>Same device id but a different board (subsystem) is a different card, not a match.</summary>
    [Fact]
    public void Match_SameDeviceDifferentSubsystem_ReturnsNull() {
        var candidates = new List<VendorPciId> { new(0x250410DE, 0x12345678, 0xA1) };
        Assert.Null(GpuPciMatcher.Match(Rtx3060, candidates));
    }

    [Fact]
    public void Match_SameDeviceDifferentRevision_ReturnsNull() {
        var candidates = new List<VendorPciId> { new(0x250410DE, 0x397D1462, 0xB2) };
        Assert.Null(GpuPciMatcher.Match(Rtx3060, candidates));
    }

    /// <summary>A driver that leaves subsystem/revision at zero still matches on the device id, rather than
    /// degrading the whole adapter to "no sensors".</summary>
    [Theory]
    [InlineData(0u, 0xA1u)]
    [InlineData(0x397D1462u, 0u)]
    [InlineData(0u, 0u)]
    public void Match_VendorOmitsSubsystemOrRevision_StillMatchesOnDeviceId(uint subSys, uint revision) {
        var candidates = new List<VendorPciId> { new(0x250410DE, subSys, revision) };
        Assert.Equal(0, GpuPciMatcher.Match(Rtx3060, candidates));
    }

    /// <summary>Two identical cards are indistinguishable by PCI id, so the claimed set pairs them in
    /// enumeration order instead of resolving both to the first.</summary>
    [Fact]
    public void Match_TwoIdenticalCards_SkipsTheAlreadyClaimedIndex() {
        var candidates = new List<VendorPciId> {
            new(0x250410DE, 0x397D1462, 0xA1),
            new(0x250410DE, 0x397D1462, 0xA1),
        };
        var first = GpuPciMatcher.Match(Rtx3060, candidates);
        Assert.Equal(0, first);

        var second = GpuPciMatcher.Match(Rtx3060, candidates, new HashSet<int> { 0 });
        Assert.Equal(1, second);

        Assert.Null(GpuPciMatcher.Match(Rtx3060, candidates, new HashSet<int> { 0, 1 }));
    }
}
