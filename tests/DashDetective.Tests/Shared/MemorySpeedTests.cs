using DashDetective.Shared;
using Xunit;

namespace DashDetective.Tests.Shared;

/// <summary>Covers <see cref="MemorySpeed"/>: which of the two figures Win32_PhysicalMemory reports is the
/// one to show. Shared by the Dashboard's RAM line and the Hardware tab's Speed row, so both describe a
/// module the same way.</summary>
public class MemorySpeedTests {
    /// <summary>The running speed wins over the rated one — with XMP/EXPO off, a DDR5-6000 kit runs at its
    /// JEDEC 4800, and that is what Task Manager reports.</summary>
    [Fact]
    public void Running_PrefersTheConfiguredSpeedOverTheRatedOne() {
        Assert.Equal(4800, MemorySpeed.Running(configuredMhz: 4800, ratedMhz: 6000));
    }

    [Fact]
    public void Running_NoConfiguredReading_FallsBackToTheRatedSpeed() {
        Assert.Equal(6000, MemorySpeed.Running(configuredMhz: 0, ratedMhz: 6000));
    }

    [Fact]
    public void Running_NeitherReported_IsZero() {
        Assert.Equal(0, MemorySpeed.Running(configuredMhz: 0, ratedMhz: 0));
    }

    /// <summary>A profile applied above the rated figure still reports what is running.</summary>
    [Fact]
    public void Running_ConfiguredAboveRated_StillWins() {
        Assert.Equal(6000, MemorySpeed.Running(configuredMhz: 6000, ratedMhz: 4800));
    }
}
