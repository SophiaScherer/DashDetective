using DashDetective.Tabs.Hardware;
using System.Globalization;
using Xunit;

namespace DashDetective.Tests.Tabs.Hardware;

/// <summary>Covers <see cref="ProcessorSpecFormatter"/>: the Processor card's clock and cache rows —
/// one decimal of GHz (unlike the live rail's two), a "Base / Boost" pair that keeps reading correctly
/// when either side is missing, and WMI's KB cache size rendered as MB.</summary>
public class ProcessorSpecFormatterTests {
    [Theory]
    [InlineData(3200, "3.2 GHz")]
    [InlineData(4700, "4.7 GHz")]
    [InlineData(3000, "3.0 GHz")]       // the trailing zero is kept, unlike the GB formatter
    [InlineData(3450, "3.5 GHz")]       // rounded to one decimal
    public void Ghz_FormatsMegahertzAsGigahertz(double mhz, string expected) {
        Assert.Equal(expected, ProcessorSpecFormatter.Ghz(mhz));
    }

    /// <summary>Both sides known: the unit is stated once, on the boost string the catalog supplies.</summary>
    [Theory]
    [InlineData(4700, "5.3 GHz", "4.7 / 5.3 GHz")]
    [InlineData(3400, "5.0 GHz", "3.4 / 5.0 GHz")]
    public void BaseBoost_WithBothSides_SharesTheUnit(double baseMhz, string boost, string expected) {
        Assert.Equal(expected, ProcessorSpecFormatter.BaseBoost(baseMhz, boost));
    }

    /// <summary>One side missing: the known side carries its own unit so the row still reads.</summary>
    [Theory]
    [InlineData(4700, null, "4.7 GHz / —")]
    [InlineData(4700, "", "4.7 GHz / —")]           // the catalog has no entry for this model
    [InlineData(0, "5.3 GHz", "— / 5.3 GHz")]       // WMI reported no base clock
    public void BaseBoost_WithOneSideMissing_KeepsTheUnitOnTheKnownSide(
        double baseMhz, string? boost, string expected) {
        Assert.Equal(expected, ProcessorSpecFormatter.BaseBoost(baseMhz, boost));
    }

    [Theory]
    [InlineData(0, null)]
    [InlineData(-100, "")]
    public void BaseBoost_WithNeitherSide_ReturnsPlaceholder(double baseMhz, string? boost) {
        Assert.Equal("—", ProcessorSpecFormatter.BaseBoost(baseMhz, boost));
    }

    [Theory]
    [InlineData(32768, "32 MB")]
    [InlineData(16384, "16 MB")]
    [InlineData(1536, "1 MB")]          // integer division truncates, as it always has
    public void CacheL3_ConvertsKilobytesToMegabytes(long kilobytes, string expected) {
        Assert.Equal(expected, ProcessorSpecFormatter.CacheL3(kilobytes));
    }

    [Theory]
    [InlineData(0)]                     // WMI reports no L3 size on some virtual CPUs
    [InlineData(-1)]
    public void CacheL3_WithNoSize_ReturnsPlaceholder(long kilobytes) {
        Assert.Equal("—", ProcessorSpecFormatter.CacheL3(kilobytes));
    }

    /// <summary>The machine's reading wins over the datasheet, e.g. a down-binned or power-limited chip.</summary>
    [Theory]
    [InlineData("4.7 GHz")]
    [InlineData(null)]
    public void BaseBoost_WithALiveReading_IgnoresTheCatalogBase(string? baseSpec) {
        Assert.Equal("4.2 / 5.3 GHz", ProcessorSpecFormatter.BaseBoost(4200, "5.3 GHz", baseSpec));
    }

    /// <summary>A VM guest has no cpufreq policy, so the datasheet supplies the base clock.</summary>
    [Theory]
    [InlineData("5.3 GHz", "4.7 / 5.3 GHz")]
    [InlineData(null, "4.7 GHz / —")]
    public void BaseBoost_WithNoLiveReading_UsesTheCatalogBase(string? boost, string expected) {
        Assert.Equal(expected, ProcessorSpecFormatter.BaseBoost(0, boost, "4.7 GHz"));
    }

    /// <summary>Neither source has it: still the placeholder, not a half-formed row.</summary>
    [Fact]
    public void BaseBoost_WithNoSourceAtAll_ReturnsPlaceholder() {
        Assert.Equal("—", ProcessorSpecFormatter.BaseBoost(0, null, null));
    }

    /// <summary>The catalog's placeholder must read as absent, not as a value.</summary>
    [Fact]
    public void BaseBoost_CatalogPlaceholders_ReadAsAbsent() {
        Assert.Equal("—", ProcessorSpecFormatter.BaseBoost(0, "—", "—"));
        Assert.Equal("4.7 GHz / —", ProcessorSpecFormatter.BaseBoost(4700, "—"));
    }

    [Fact]
    public void CacheL3_WithNoLiveSize_UsesTheCatalogSize() {
        Assert.Equal("32 MB", ProcessorSpecFormatter.CacheL3(0, "32 MB"));
    }

    [Fact]
    public void CacheL3_WithALiveSize_IgnoresTheCatalogSize() {
        Assert.Equal("16 MB", ProcessorSpecFormatter.CacheL3(16384, "32 MB"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("—")]
    public void CacheL3_WithNeitherSource_ReturnsPlaceholder(string? spec) {
        Assert.Equal("—", ProcessorSpecFormatter.CacheL3(0, spec));
    }

    [Theory]
    [InlineData("AM5", "AM5")]
    [InlineData(null, "—")]
    [InlineData("", "—")]
    [InlineData("—", "—")]
    public void Spec_RendersACatalogFieldOrThePlaceholder(string? value, string expected) {
        Assert.Equal(expected, ProcessorSpecFormatter.Spec(value));
    }

    /// <summary>The firmware's socket designation is never replaced by a rated one.</summary>
    [Theory]
    [InlineData("AM5")]
    [InlineData(null)]
    public void Socket_WithALiveDesignation_IgnoresTheCatalogSocket(string? spec) {
        Assert.Equal("SocketAM5", ProcessorSpecFormatter.Socket("SocketAM5", spec));
    }

    [Theory]
    [InlineData(null, "AM5", "AM5")]
    [InlineData("", "AM5", "AM5")]
    [InlineData("  ", "AM5", "AM5")]
    [InlineData(null, null, "—")]
    [InlineData("", "—", "—")]
    public void Socket_WithNoLiveDesignation_FallsBackToTheCatalog(string? live, string? spec, string expected) {
        Assert.Equal(expected, ProcessorSpecFormatter.Socket(live, spec));
    }

    /// <summary>The paired row states the unit once, on the right; non-GHz strings pass through.</summary>
    [Theory]
    [InlineData("4.7 GHz", "4.7")]
    [InlineData("4.7", "4.7")]
    [InlineData("—", "—")]
    [InlineData("", "")]
    public void WithoutGhz_DropsOnlyTheTrailingUnit(string clock, string expected) {
        Assert.Equal(expected, ProcessorSpecFormatter.WithoutGhz(clock));
    }

    /// <summary>The decimal separator is a period regardless of the ambient culture, matching the rest of
    /// the app's InvariantCulture formatting.</summary>
    [Fact]
    public void Ghz_UnderACommaDecimalCulture_StillUsesAPeriod() {
        var previous = CultureInfo.CurrentCulture;
        try {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            Assert.Equal("4.7 GHz", ProcessorSpecFormatter.Ghz(4700));
            Assert.Equal("4.7 / 5.3 GHz", ProcessorSpecFormatter.BaseBoost(4700, "5.3 GHz"));
        } finally {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
