using DashDetective.Tabs.Hardware;
using System.Globalization;
using Xunit;

namespace DashDetective.Tests.Tabs.Hardware;

/// <summary>Covers <see cref="MemorySpecFormatter"/>: the Memory card's subtitle and spec rows — whole
/// capacities losing their trailing ".0", the uniform vs mixed module layouts, millivolts rendered as
/// volts, and an unrecognised SMBIOS code degrading to the generic "RAM" rather than blanking.</summary>
public class MemorySpecFormatterTests {
    [Theory]
    [InlineData(16, "16")]              // whole values drop the decimal entirely
    [InlineData(32, "32")]
    [InlineData(1.5, "1.5")]
    [InlineData(0.5, "0.5")]
    public void Gb_DropsTheDecimalOnlyForWholeValues(double gb, string expected) {
        Assert.Equal(expected, MemorySpecFormatter.Gb(gb));
    }

    [Theory]
    [InlineData(32, "DDR5", 6000, "32 GB DDR5-6000")]
    [InlineData(16, "DDR4", 3200, "16 GB DDR4-3200")]
    [InlineData(32, "DDR5", 0, "32 GB DDR5")]       // no readable speed: the type still stands alone
    public void Summary_AppendsTheSpeedOnlyWhenKnown(
        double totalGb, string type, int speed, string expected) {
        Assert.Equal(expected, MemorySpecFormatter.Summary(totalGb, type, speed));
    }

    /// <summary>Matching modules collapse to a count; mismatched ones are listed so an odd stick shows.</summary>
    [Theory]
    [InlineData(new[] { 16d, 16d }, "2 × 16 GB")]
    [InlineData(new[] { 32d }, "1 × 32 GB")]
    [InlineData(new[] { 8d, 8d, 8d, 8d }, "4 × 8 GB")]
    [InlineData(new[] { 16d, 8d }, "16 GB + 8 GB")]
    [InlineData(new[] { 16d, 16d, 8d }, "16 GB + 16 GB + 8 GB")]
    public void Modules_CollapsesUniformLayoutsAndListsMixedOnes(double[] moduleGbs, string expected) {
        Assert.Equal(expected, MemorySpecFormatter.Modules(moduleGbs));
    }

    [Fact]
    public void Modules_WithNoModules_ReturnsPlaceholder() {
        Assert.Equal("—", MemorySpecFormatter.Modules([]));
    }

    [Theory]
    [InlineData(6000, "6000 MT/s")]
    [InlineData(0, "—")]                // the module reported neither a configured nor a rated speed
    public void Speed_FormatsTheTransferRateOrPlaceholder(int mts, string expected) {
        Assert.Equal(expected, MemorySpecFormatter.Speed(mts));
    }

    [Theory]
    [InlineData(2, 4, "2 / 4")]
    [InlineData(4, 4, "4 / 4")]
    [InlineData(2, 0, "2")]             // Win32_PhysicalMemoryArray unreadable: show what is populated
    public void SlotsUsed_FallsBackToThePopulatedCountAlone(int populated, int total, string expected) {
        Assert.Equal(expected, MemorySpecFormatter.SlotsUsed(populated, total));
    }

    [Theory]
    [InlineData(1350, "1.35 V")]
    [InlineData(1200, "1.2 V")]         // trailing zeros are dropped
    [InlineData(1100, "1.1 V")]
    [InlineData(0, "—")]
    public void Voltage_ConvertsMillivoltsToVolts(int millivolts, string expected) {
        Assert.Equal(expected, MemorySpecFormatter.Voltage(millivolts));
    }

    [Theory]
    [InlineData(34, "DDR5")]
    [InlineData(26, "DDR4")]
    [InlineData(24, "DDR3")]
    [InlineData(35, "LPDDR5")]
    [InlineData(0, "RAM")]              // SMBIOS reports 0 when the type is unknown
    [InlineData(99, "RAM")]             // a code newer than this table still names something
    public void TypeLabel_MapsSmbiosCodesAndFallsBackToRam(int smbiosType, string expected) {
        Assert.Equal(expected, MemorySpecFormatter.TypeLabel(smbiosType));
    }

    /// <summary>The decimal separator is a period regardless of the ambient culture, matching the rest of
    /// the app's InvariantCulture formatting.</summary>
    [Fact]
    public void UnderACommaDecimalCulture_StillUsesAPeriod() {
        var previous = CultureInfo.CurrentCulture;
        try {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            Assert.Equal("1.5", MemorySpecFormatter.Gb(1.5));
            Assert.Equal("1.35 V", MemorySpecFormatter.Voltage(1350));
        } finally {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
