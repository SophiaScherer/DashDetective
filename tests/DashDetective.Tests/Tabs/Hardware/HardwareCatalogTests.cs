using DashDetective.Tabs.Hardware.Catalog;
using System.Collections.Generic;
using Xunit;

namespace DashDetective.Tests.Tabs.Hardware;

/// <summary>Covers <see cref="HardwareCatalog"/>: the <c>Normalize</c> stripping rules and the
/// <c>Match</c> resolution order (exact, then a token-aligned key inside the name with longest-key-wins,
/// else null), including the guards that stop a near-miss inheriting another part's datasheet. The
/// <c>Match</c> algorithm is exercised with synthetic normalized tables so the assertions don't depend on
/// the shipped catalog data; a couple of real <c>Lookup</c> calls smoke-test the wiring.</summary>
public class HardwareCatalogTests {
    // --- Normalize ---

    [Fact]
    public void Normalize_StripsTrademarkParenthesesAndSymbols() {
        Assert.Equal("INTEL CORE I7", HardwareCatalog.Normalize("Intel(R) Core(TM) i7"));
        Assert.Equal("AMD RYZEN 5", HardwareCatalog.Normalize("AMD® Ryzen™ 5"));
    }

    [Fact]
    public void Normalize_StripsCpuAndProcessorWords() {
        Assert.Equal("INTEL CORE I5", HardwareCatalog.Normalize("Intel Core i5 CPU Processor"));
    }

    [Fact]
    public void Normalize_StripsClockSuffix() {
        Assert.Equal("AMD RYZEN 5 7600X", HardwareCatalog.Normalize("AMD Ryzen 5 7600X @ 4.70GHz"));
    }

    [Fact]
    public void Normalize_StripsIntegratedGpuSuffix() {
        Assert.Equal("AMD RYZEN 5 5600G", HardwareCatalog.Normalize("AMD Ryzen 5 5600G with Radeon Graphics"));
    }

    [Fact]
    public void Normalize_CollapsesNonAlphanumericRunsAndTrims() {
        Assert.Equal("FOO BAR BAZ", HardwareCatalog.Normalize("  Foo--Bar__Baz  "));
    }

    // --- Match (synthetic tables) ---

    [Fact]
    public void Match_ExactNormalizedKey_ReturnsThatValue() {
        var table = new Dictionary<string, string> { ["RTX 4070"] = "base" };
        Assert.Equal("base", HardwareCatalog.Match(table, "RTX 4070"));
    }

    [Fact]
    public void Match_LongestKeyWins_VariantNotShadowedByBase() {
        var table = new Dictionary<string, string> {
            ["RTX 4070"] = "base",
            ["RTX 4070 TI"] = "variant",
        };
        Assert.Equal("variant", HardwareCatalog.Match(table, "NVIDIA GeForce RTX 4070 Ti"));
        Assert.Equal("base", HardwareCatalog.Match(table, "NVIDIA GeForce RTX 4070"));
    }

    [Fact]
    public void Match_LongestKeyWins_SuffixDigitsDistinguishModels() {
        var table = new Dictionary<string, string> {
            ["7600"] = "base",
            ["7600X"] = "x-variant",
        };
        Assert.Equal("x-variant", HardwareCatalog.Match(table, "AMD Ryzen 5 7600X"));
        Assert.Equal("base", HardwareCatalog.Match(table, "AMD Ryzen 5 7600"));
    }

    /// <summary>A name SHORTER than the key must not select it: a board reporting a bare "B650" would
    /// otherwise inherit a "B650E …" entry's form factor and M.2 count, and a truncated memory part number
    /// another kit's timings. The name has to identify the part, not merely resemble it.</summary>
    [Fact]
    public void Match_NameShorterThanKey_ReturnsNull() {
        var boards = new Dictionary<string, string> { ["B650E AORUS MASTER"] = "spec" };
        Assert.Null(HardwareCatalog.Match(boards, "B650"));

        var memory = new Dictionary<string, string> { ["F5 6000J3636F16G"] = "timings" };
        Assert.Null(HardwareCatalog.Match(memory, "F5 6000"));
    }

    /// <summary>A key must align to token boundaries, so it can't match inside a longer model token.</summary>
    [Fact]
    public void Match_KeyInsideALongerToken_DoesNotMatch() {
        var table = new Dictionary<string, string> { ["RTX 4060"] = "v" };
        Assert.Null(HardwareCatalog.Match(table, "NVIDIA GeForce RTX 40600"));
        Assert.Equal("v", HardwareCatalog.Match(table, "NVIDIA GeForce RTX 4060"));
    }

    /// <summary>A mobile part shares the desktop card's model number but has its own memory, clocks and
    /// core counts, so it must fall through to "—" rather than borrow the desktop datasheet.</summary>
    [Theory]
    [InlineData("NVIDIA GeForce RTX 4060 Laptop GPU")]
    [InlineData("NVIDIA GeForce RTX 4060 Mobile")]
    [InlineData("NVIDIA GeForce RTX 4060 Max-Q")]
    public void Match_MobileVariant_DoesNotInheritTheDesktopSpec(string name) {
        var table = new Dictionary<string, string> { ["RTX 4060"] = "desktop" };
        Assert.Null(HardwareCatalog.Match(table, name));
    }

    /// <summary>A catalog entry for the mobile part itself still resolves.</summary>
    [Fact]
    public void Match_MobileVariant_ResolvesItsOwnEntry() {
        var table = new Dictionary<string, string> {
            ["RTX 4060"] = "desktop",
            ["RTX 4060 LAPTOP"] = "mobile",
        };
        Assert.Equal("mobile", HardwareCatalog.Match(table, "NVIDIA GeForce RTX 4060 Laptop GPU"));
    }

    [Fact]
    public void Match_NoOverlap_ReturnsNull() {
        var table = new Dictionary<string, string> { ["RTX 4070"] = "v" };
        Assert.Null(HardwareCatalog.Match(table, "GTX 1080"));
    }

    [Fact]
    public void Match_EmptyTable_ReturnsNull() {
        Assert.Null(HardwareCatalog.Match(new Dictionary<string, string>(), "RTX 4070"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Match_BlankRaw_ReturnsNull(string raw) {
        var table = new Dictionary<string, string> { ["RTX 4070"] = "v" };
        Assert.Null(HardwareCatalog.Match(table, raw));
    }

    // --- Real Lookup smoke ---

    [Fact]
    public void LookupGpu_KnownModel_ResolvesFromShippedCatalog() {
        var spec = HardwareCatalog.LookupGpu("NVIDIA GeForce RTX 4070 Ti");
        Assert.NotNull(spec);
        Assert.Equal("7,680", spec!.CudaCores);   // the Ti variant, not the base 4070's 5,888
    }

    [Fact]
    public void LookupGpu_UnknownModel_ReturnsNull() {
        Assert.Null(HardwareCatalog.LookupGpu("Definitely Not A Real GPU 9999"));
    }

    /// <summary>The three columns added so a machine that reports none of them still fills the row. Read
    /// from the shipped table on purpose: the point is that the data is there, not that Match works.</summary>
    [Fact]
    public void LookupCpu_KnownModel_CarriesTheFallbackColumnsToo() {
        var spec = HardwareCatalog.LookupCpu("AMD Ryzen 5 7600X 6-Core Processor");

        Assert.NotNull(spec);
        Assert.Equal("4.7 GHz", spec!.Base);
        Assert.Equal("32 MB", spec.CacheL3);
        Assert.Equal("AM5", spec.Socket);
    }

    /// <summary>Every shipped entry carries all five columns. A half-filled row would render as a silent
    /// "—" that looks exactly like a part the table has never heard of.</summary>
    [Fact]
    public void CpuCatalog_EveryEntry_FillsEveryColumn() {
        foreach (var (key, spec) in CpuCatalog.Data) {
            Assert.False(string.IsNullOrWhiteSpace(spec.Boost), key);
            Assert.False(string.IsNullOrWhiteSpace(spec.Tdp), key);
            Assert.False(string.IsNullOrWhiteSpace(spec.Base), key);
            Assert.False(string.IsNullOrWhiteSpace(spec.CacheL3), key);
            Assert.False(string.IsNullOrWhiteSpace(spec.Socket), key);
        }
    }

    /// <summary>A rated base clock below its own boost, on every part. Cheap, but it is the one check that
    /// catches the two columns being transposed on a hand-entered row.</summary>
    [Fact]
    public void CpuCatalog_EveryEntry_RatesBaseBelowBoost() {
        foreach (var (key, spec) in CpuCatalog.Data)
            Assert.True(Ghz(spec.Base) < Ghz(spec.Boost), $"{key}: {spec.Base} / {spec.Boost}");
    }

    private static double Ghz(string clock) =>
        double.Parse(
            clock.Replace(" GHz", ""), System.Globalization.CultureInfo.InvariantCulture);
}
